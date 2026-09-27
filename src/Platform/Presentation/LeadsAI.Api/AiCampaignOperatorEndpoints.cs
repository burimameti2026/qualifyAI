using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api;

public static class AiCampaignOperatorEndpoints
{
    public static IEndpointRouteBuilder MapAiCampaignOperator(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/ai-campaign-operator");

        g.MapPost("/tenants/{tenantId:guid}/prepare",
            async (Guid tenantId, AiCampaignBrief input, AppDbContext db,
                IAutonomousAcquisitionTemplateRegistry templates, CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(input.Brief))
                    return Results.BadRequest(new { error = "brief_required" });

                var parsed = ParseBrief(input.Brief);
                var pack = await ResolvePackAsync(db, parsed.Industry, ct);
                var template = ResolveTemplate(templates, parsed.Industry);

                var actions = new List<string>();

                var icp = await db.IcpProfiles
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId &&
                                              x.Active &&
                                              x.Industry == parsed.Industry, ct);

                if (icp is null)
                {
                    icp = new IcpProfile
                    {
                        TenantId = tenantId,
                        Name = $"{parsed.Industry} AI ICP",
                        Industry = parsed.Industry,
                        CountriesCsv = string.Join(",", parsed.Countries),
                        IntentKeywordsCsv = string.Join(",", template.Keywords),
                        CriteriaJson = JsonSerializer.Serialize(new { brief = input.Brief }),
                        Active = true
                    };
                    db.IcpProfiles.Add(icp);
                    actions.Add("created ICP");
                }

                var targetName = input.TargetListName ?? $"{parsed.Industry} AI Prospects";
                var target = await db.TargetLists.FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.Name == targetName, ct);

                if (target is null)
                {
                    target = new TargetList
                    {
                        TenantId = tenantId,
                        Name = targetName,
                        IcpProfileId = icp.Id,
                        Dynamic = true,
                        Description = $"AI-generated target list from: {input.Brief}"
                    };
                    db.TargetLists.Add(target);
                    actions.Add("created target list");
                }
                else
                {
                    target.IcpProfileId = icp.Id;
                }

                var campaignName = input.CampaignName ?? $"{parsed.Industry} Prospecting";
                var campaign = await db.Campaigns.FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.Name == campaignName &&
                         x.Status != CampaignStatus.Completed, ct);

                if (campaign is null)
                {
                    campaign = new Campaign
                    {
                        TenantId = tenantId,
                        TargetListId = target.Id,
                        Name = campaignName,
                        Goal = input.Goal ?? "book-demo",
                        SenderName = input.SenderName ?? string.Empty,
                        SenderEmail = input.SenderEmail ?? string.Empty,
                        StartsAtUtc = DateTime.UtcNow
                    };
                    db.Campaigns.Add(campaign);
                    actions.Add("created campaign");
                }
                else
                {
                    campaign.TargetListId = target.Id;
                }

                if (!await db.CampaignSteps.AnyAsync(x => x.TenantId == tenantId && x.CampaignId == campaign.Id, ct))
                {
                    db.CampaignSteps.AddRange(
                        new CampaignStep
                        {
                            TenantId = tenantId, CampaignId = campaign.Id, StepNumber = 1,
                            DelayHours = 0, Channel = "email",
                            SubjectTemplate = "Quick question about {{company}}",
                            BodyTemplate = "Hi {{contact}},\n\nI noticed {{company}}. Would it be useful to compare how similar teams handle this today?"
                        },
                        new CampaignStep
                        {
                            TenantId = tenantId, CampaignId = campaign.Id, StepNumber = 2,
                            DelayHours = 48, Channel = "email",
                            SubjectTemplate = "Re: quick question about {{company}}",
                            BodyTemplate = "Just following up in case this is relevant to your team."
                        });
                    actions.Add("created outreach workflow");
                }

                var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
                    x => x.TenantId == tenantId &&
                         x.Industry == parsed.Industry &&
                         x.Status != AutonomousAgentStatus.Stopped, ct);

                if (agent is null)
                {
                    agent = new AutonomousAcquisitionAgent
                    {
                        TenantId = tenantId,
                        Name = $"{parsed.Industry} Acquisition Agent",
                        TemplateCode = template.Code,
                        Industry = parsed.Industry,
                        Region = template.Region,
                        CountriesJson = JsonSerializer.Serialize(parsed.Countries),
                        IcpJson = JsonSerializer.Serialize(new { icpId = icp.Id, brief = input.Brief }),
                        MinimumScore = template.MinimumScore,
                        DailyDiscoveryLimit = input.DailyDiscoveryLimit is > 0 and <= 100 ? input.DailyDiscoveryLimit : 50,
                        DailyEmailLimit = input.DailyEmailLimit is > 0 and <= 100 ? input.DailyEmailLimit : 10,
                        Status = AutonomousAgentStatus.Active
                    };
                    db.AutonomousAcquisitionAgents.Add(agent);
                    actions.Add("created and activated acquisition agent");
                }
                else if (agent.Status == AutonomousAgentStatus.Paused || agent.Status == AutonomousAgentStatus.Failed)
                {
                    agent.Status = AutonomousAgentStatus.Active;
                    actions.Add("reactivated acquisition agent");
                }

                bool packInstalled = false;
                if (pack is not null)
                {
                    var installed = await db.TenantIndustryPacks.AnyAsync(
                        x => x.TenantId == tenantId && x.IndustryPackId == pack.Id && x.Enabled, ct);
                    if (!installed)
                    {
                        db.TenantIndustryPacks.Add(new TenantIndustryPack
                        {
                            TenantId = tenantId,
                            IndustryPackId = pack.Id,
                            Enabled = true,
                            OverridesJson = "{}"
                        });
                        actions.Add($"installed {pack.Code} industry pack");
                    }
                    packInstalled = true;
                }

                campaign.AgentId = agent.Id;
                campaign.PackageCode = pack?.Code ?? template.Code;
                campaign.PackageVersion = "1.0";
                campaign.Objective = input.Brief.Trim();
                campaign.PlanStatus = "ready";
                campaign.PlanJson = JsonSerializer.Serialize(new { source = "ai-campaign-operator", brief = input.Brief, industry = parsed.Industry, countries = parsed.Countries, template = template.Code });

                await db.SaveChangesAsync(ct);

                var checks = new[]
                {
                    new { name = "pack", status = packInstalled ? "ok" : "missing" },
                    new { name = "icp", status = "ok" },
                    new { name = "target-list", status = "ok" },
                    new { name = "agent", status = "ok" },
                    new { name = "workflow", status = "ok" },
                    new { name = "runtime", status = agent.Status == AutonomousAgentStatus.Active ? "ok" : "fixed" }
                };

                return Results.Ok(new
                {
                    status = checks.All(x => x.status is "ok" or "fixed") ? "ready" : "needs-attention",
                    campaignId = campaign.Id,
                    targetListId = target.Id,
                    icpId = icp.Id,
                    agentId = agent.Id,
                    pack = pack is null ? null : new { pack.Id, pack.Code, pack.Name, installed = packInstalled },
                    checks,
                    actions,
                    canStart = checks.All(x => x.status is "ok" or "fixed")
                });
            });

        g.MapPost("/tenants/{tenantId:guid}/campaigns/{campaignId:guid}/start",
            async (Guid tenantId, Guid campaignId, AppDbContext db,
                IAutonomousAcquisitionRunOrchestrator orchestrator, CancellationToken ct) =>
            {
                var campaign = await db.Campaigns.FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.Id == campaignId, ct);
                if (campaign is null) return Results.NotFound(new { error = "campaign_not_found" });

                if (!campaign.AgentId.HasValue)
                    return Results.Conflict(new { error = "campaign_agent_not_ready" });

                var agent = await db.AutonomousAcquisitionAgents
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value && x.Status != AutonomousAgentStatus.Stopped, ct);

                if (agent is null)
                    return Results.Conflict(new { error = "agent_not_ready" });

                campaign.Start();

                var prospectIds = await db.TargetListMembers
                    .Where(x => x.TenantId == tenantId && x.TargetListId == campaign.TargetListId)
                    .Select(x => x.ProspectId)
                    .ToListAsync(ct);

                var existing = await db.CampaignRecipients
                    .Where(x => x.TenantId == tenantId && x.CampaignId == campaignId)
                    .Select(x => x.ProspectId)
                    .ToListAsync(ct);

                db.CampaignRecipients.AddRange(prospectIds.Except(existing).Select(id =>
                    new CampaignRecipient
                    {
                        TenantId = tenantId, CampaignId = campaignId, ProspectId = id,
                        CurrentStep = 0, Status = "active",
                        NextRunAtUtc = DateTime.UtcNow
                    }));

                var run = new AutonomousAcquisitionAgentRun
                {
                    TenantId = tenantId, AgentId = agent.Id, CampaignId = campaignId, IsManual = false,
                    Status = AutonomousAgentRunStatus.Queued,
                    ScheduledAtUtc = DateTime.UtcNow,
                    Query = $"campaign:{campaignId}"
                };
                db.AutonomousAcquisitionAgentRuns.Add(run);
                await db.SaveChangesAsync(ct);

                return Results.Accepted(
                    $"/api/ai-campaign-operator/tenants/{tenantId}/campaigns/{campaignId}/status",
                    new { campaignId, status = campaign.Status, agentId = agent.Id, runId = run.Id });
            });

        g.MapGet("/tenants/{tenantId:guid}/campaigns/{campaignId:guid}/status",
            async (Guid tenantId, Guid campaignId, AppDbContext db, CancellationToken ct) =>
            {
                var campaign = await db.Campaigns.AsNoTracking().FirstOrDefaultAsync(
                    x => x.TenantId == tenantId && x.Id == campaignId, ct);
                if (campaign is null) return Results.NotFound();

                var active = await db.CampaignRecipients.CountAsync(x => x.TenantId == tenantId && x.CampaignId == campaignId && x.Status == "active", ct);
                var awaitingDelivery = await db.CampaignRecipients.CountAsync(x => x.TenantId == tenantId && x.CampaignId == campaignId && x.Status == "awaiting-delivery", ct);
                var completed = await db.CampaignRecipients.CountAsync(x => x.TenantId == tenantId && x.CampaignId == campaignId && x.Status == "completed", ct);
                var failed = await db.CampaignRecipients.CountAsync(x => x.TenantId == tenantId && x.CampaignId == campaignId && x.Status == "failed", ct);
                var recipients = await db.CampaignRecipients.CountAsync(
                    x => x.TenantId == tenantId && x.CampaignId == campaignId, ct);
                var sent = await db.OutreachMessages.CountAsync(
                    x => x.TenantId == tenantId && x.CampaignId == campaignId &&
                         (x.Status == OutreachStatus.Sent || x.Status == OutreachStatus.Delivered), ct);
                var replied = await db.ProspectReplies.CountAsync(
                    x => x.TenantId == tenantId && x.CampaignId == campaignId, ct);

                var run = await db.AutonomousAcquisitionAgentRuns.AsNoTracking()
                    .Where(x => x.TenantId == tenantId && x.Query == $"campaign:{campaignId}")
                    .OrderByDescending(x => x.ScheduledAtUtc)
                    .FirstOrDefaultAsync(ct);

                var agent = run is null ? null : await db.AutonomousAcquisitionAgents.AsNoTracking()
                    .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == run.AgentId, ct);

                return Results.Ok(new
                {
                    campaign = new { campaign.Id, campaign.Name, campaign.Status, campaign.StartsAtUtc },
                    agent = agent is null ? null : new { agent.Id, agent.Name, agent.Status, agent.TemplateCode, agent.LastRunAtUtc },
                    run,
                    metrics = new { recipients, active, awaitingDelivery, completed, failed, sent, replied },
                    aiStatus = run?.Status switch
                    {
                        AutonomousAgentRunStatus.Completed => "completed",
                        AutonomousAgentRunStatus.Running => "running",
                        AutonomousAgentRunStatus.Failed => "attention",
                        _ => campaign.Status == CampaignStatus.Running ? "queued" : "ready"
                    }
                });
            });

        return app;
    }

    private static async Task<IndustryPack?> ResolvePackAsync(AppDbContext db, string industry, CancellationToken ct)
    {
        var exact = await db.IndustryPacks.FirstOrDefaultAsync(
            x => x.Code == industry.ToLowerInvariant(), ct);
        if (exact is not null) return exact;

        var packs = await db.IndustryPacks.AsNoTracking().ToListAsync(ct);
        return packs
            .OrderBy(x => x.Code.Equals("logistics", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .FirstOrDefault(x => industry.Contains(x.Code, StringComparison.OrdinalIgnoreCase) ||
                                 x.Name.Contains(industry, StringComparison.OrdinalIgnoreCase));
    }

    private static AutonomousAcquisitionTemplate ResolveTemplate(
        IAutonomousAcquisitionTemplateRegistry registry, string industry)
    {
        var code = industry.ToLowerInvariant();
        try { return registry.Resolve(code); }
        catch { return registry.List().FirstOrDefault() ??
            new AutonomousAcquisitionTemplate("custom", "Custom", industry, "Europe", Array.Empty<string>(), Array.Empty<string>(), 70); }
    }

    private static ParsedBrief ParseBrief(string brief)
    {
        var text = brief.ToLowerInvariant();
        var industry = text.Contains("logistic") || text.Contains("3pl") || text.Contains("freight")
            ? "logistics"
            : text.Contains("ecommerce") || text.Contains("e-commerce")
                ? "ecommerce"
                : text.Contains("agency")
                    ? "agency"
                    : text.Contains("saas") || text.Contains("software")
                        ? "saas"
                        : "general";

        var countries = new List<string>();
        foreach (var country in new[] { "germany", "deutschland", "france", "italy", "spain", "netherlands", "belgium", "uk", "united kingdom", "austria", "switzerland", "poland" })
            if (text.Contains(country))
                countries.Add(country == "deutschland" ? "Germany" : country);

        if (countries.Count == 0) countries.Add("Europe");

        return new ParsedBrief(industry, countries.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private sealed record ParsedBrief(string Industry, string[] Countries);
}

public sealed record AiCampaignBrief(
    string Brief,
    string? CampaignName = null,
    string? TargetListName = null,
    string? Goal = null,
    string? SenderName = null,
    string? SenderEmail = null,
    int DailyDiscoveryLimit = 50,
    int DailyEmailLimit = 10);
