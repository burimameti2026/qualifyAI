using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Infrastructure.WorkspacePackages;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api;

public static class AutonomousAcquisitionEndpoints
{
    public static IEndpointRouteBuilder MapAutonomousAcquisition(this IEndpointRouteBuilder endpoints)
    {
        var g = endpoints.MapGroup("/api/autonomous-acquisition");

        g.AddEndpointFilter(async (ctx, next) =>
        {
            if (ctx.HttpContext.Request.RouteValues.TryGetValue("tenantId", out var raw) &&
                Guid.TryParse(raw?.ToString(), out var routeTenant))
            {
                var current = ctx.HttpContext.RequestServices.GetRequiredService<ITenantContext>().TenantId();
                if (routeTenant != current) return Results.Forbid();
            }
            return await next(ctx);
        });

        g.MapGet("/templates", (IAutonomousAcquisitionTemplateRegistry r) => Results.Ok(r.List()));

        g.MapPost("/tenants/{tenantId}/campaigns", async (
            Guid tenantId,
            CreateCampaignRequest input,
            AppDbContext db,
            IAutonomousAcquisitionTemplateRegistry templates,
            IAutonomousAcquisitionWorkflowPlanner planner,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(input.Name))
                return Results.BadRequest(new { error = "Campaign name is required." });

            var packageCode = string.IsNullOrWhiteSpace(input.PackageCode) ? "custom" : input.PackageCode.Trim();
            var pack = await db.IndustryPacks
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Code == packageCode, ct);
            if (pack is null)
                return Results.BadRequest(new { error = $"Industry Pack '{packageCode}' is not configured." });

            var installed = await db.TenantIndustryPacks
                .AnyAsync(x => x.TenantId == tenantId && x.IndustryPackId == pack.Id && x.Enabled, ct);
            if (!installed)
                return Results.Conflict(new { error = $"Industry Pack '{packageCode}' is not installed for this tenant." });

            var template = templates.Resolve(packageCode);
            var agent = new AutonomousAcquisitionAgent
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = input.Name + " Agent",
                TemplateCode = template.Code,
                Industry = input.Industry ?? template.Industry,
                Region = input.Region ?? template.Region,
                CountriesJson = JsonSerializer.Serialize(input.Countries ?? Array.Empty<string>()),
                IcpJson = JsonSerializer.Serialize(input.Icp ?? new Dictionary<string, string>()),
                MinimumScore = input.MinimumScore > 0 ? Math.Clamp(input.MinimumScore, 0, 100) : template.MinimumScore,
                DailyDiscoveryLimit = Math.Clamp(input.DailyDiscoveryLimit <= 0 ? 50 : input.DailyDiscoveryLimit, 1, 100),
                DailyEmailLimit = Math.Clamp(input.DailyEmailLimit <= 0 ? 10 : input.DailyEmailLimit, 1, 1000)
            };

            templates.Apply(agent);

            var icp = new IcpProfile
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = input.Name + " ICP",
                Industry = input.Industry ?? template.Industry,
                CountriesCsv = string.Join(',', input.Countries ?? Array.Empty<string>()),
                IntentKeywordsCsv = string.Join(',', template.Keywords),
                CriteriaJson = JsonSerializer.Serialize(input.Icp ?? new Dictionary<string, string>()),
                Active = true
            };

            var campaignId = Guid.NewGuid();

            var target = new TargetList
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CampaignId = campaignId,
                Name = input.Name + " targets",
                IcpProfileId = icp.Id,
                Description = template.TargetDefinition,
                Dynamic = true
            };

            var campaign = new Campaign
            {
                Id = campaignId,
                TenantId = tenantId,
                TargetListId = target.Id,
                AgentId = agent.Id,
                PackageCode = template.Code,
                PackageVersion = "1.0",
                Objective = input.Objective ?? template.Description,
                Name = input.Name,
                SenderName = input.SenderName ?? string.Empty,
                SenderEmail = input.SenderEmail ?? string.Empty,
                PlanStatus = "draft"
            };

            db.IcpProfiles.Add(icp);
            db.TargetLists.Add(target);
            db.AutonomousAcquisitionAgents.Add(agent);
            db.Campaigns.Add(campaign);
            await db.SaveChangesAsync(ct);

            var tasks = await planner.EnsurePlanAsync(agent, template, ct);
            campaign.PlanJson = JsonSerializer.Serialize(new
            {
                package = new { template.Code, template.Name, template.Description, template.ProspectType, template.TargetDefinition },
                objective = campaign.Objective,
                agent = new { agent.Id, agent.Name },
                workflow = new
                {
                    name = $"{template.Name} acquisition workflow",
                    steps = tasks.Select(t => new
                    {
                        sequence = t.Sequence,
                        key = t.Type,
                        name = t.Name,
                        purpose = ReadTaskPurpose(t.ConfigurationJson),
                        input = ReadTaskInput(t.ConfigurationJson),
                        nextStep = ReadTaskNext(t.ConfigurationJson),
                        requiresApproval = t.RequiresApproval
                    })
                },
                tasks = tasks.Select(t => new { t.Id, t.Sequence, t.Type, t.Name, t.Status, t.RequiresApproval, t.ConfigurationJson, t.ResultJson }),
                messages = template.OutreachTemplates.Select(m => new { m.Step, m.Name, m.Subject, m.Body, m.DelayHours, m.RequiresApproval })
            });
            campaign.PlanStatus = "ready";
            await db.SaveChangesAsync(ct);

            return Results.Created(
                $"/api/autonomous-acquisition/tenants/{tenantId}/campaigns/{campaign.Id}",
                new { campaign, agent, package = template, icp, tasks, target });
        });

        g.MapGet("/tenants/{tenantId}/campaigns", async (
            Guid tenantId, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Campaigns
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Take(100)
                .ToListAsync(ct)));

        g.MapGet("/tenants/{tenantId}/campaigns/{id}/plan", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var campaign = await db.Campaigns
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
            if (campaign is null) return Results.NotFound();

            var agent = campaign.AgentId.HasValue
                ? await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(
                    x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value, ct)
                : null;

            var latestRun = agent is null
                ? null
                : await db.AutonomousAcquisitionAgentRuns
                    .Where(x => x.TenantId == tenantId && x.AgentId == agent.Id && x.CampaignId == campaign.Id)
                    .OrderByDescending(x => x.ScheduledAtUtc)
                    .FirstOrDefaultAsync(ct);

            IReadOnlyList<AutonomousAcquisitionTask> tasks = agent is null
                ? Array.Empty<AutonomousAcquisitionTask>()
                : await db.AutonomousAcquisitionTasks
                    .Where(x => x.TenantId == tenantId && x.AgentId == agent.Id &&
                                (latestRun != null ? x.RunId == latestRun.Id : x.RunId == null))
                    .OrderBy(x => x.Sequence)
                    .ToListAsync(ct);

            var steps = await db.CampaignSteps
                .Where(x => x.TenantId == tenantId && x.CampaignId == campaign.Id)
                .OrderBy(x => x.StepNumber)
                .ToListAsync(ct);

            return Results.Ok(new
            {
                campaign,
                latestRun,
                currentStep = tasks.FirstOrDefault(x => x.Status is AutonomousAgentTaskStatus.Running or AutonomousAgentTaskStatus.Pending),
                packageCode = campaign.PackageCode,
                agent,
                workflow = new
                {
                    name = agent is null ? "Campaign workflow" : $"{agent.Name} workflow",
                    steps = tasks.Select(t => new
                    {
                        sequence = t.Sequence,
                        key = t.Type,
                        name = t.Name,
                        purpose = ReadTaskPurpose(t.ConfigurationJson),
                        input = ReadTaskInput(t.ConfigurationJson),
                        status = t.Status,
                        nextStep = ReadTaskNext(t.ConfigurationJson),
                        requiresApproval = t.RequiresApproval,
                        result = t.ResultJson,
                        error = t.Error
                    })
                },
                tasks,
                steps
            });
        });

        g.MapPost("/tenants/{tenantId}/campaigns/{id}/start", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
            if (campaign is null) return Results.NotFound();
            if (campaign.Status is CampaignStatus.Stopped or CampaignStatus.Completed)
                return Results.BadRequest(new { error = $"Campaign is {campaign.Status}." });
            if (!campaign.AgentId.HasValue)
                return Results.BadRequest(new { error = "Campaign has no provisioned workflow agent." });

            var agent = await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value, ct);
            if (agent is null)
                return Results.BadRequest(new { error = "Campaign workflow agent is missing." });

            campaign.Start();
            agent.Status = AutonomousAgentStatus.Active;
            agent.UpdatedAtUtc = DateTime.UtcNow;

            var existingQueued = await db.AutonomousAcquisitionAgentRuns.AnyAsync(
                x => x.TenantId == tenantId && x.CampaignId == campaign.Id &&
                     x.Status is AutonomousAgentRunStatus.Queued or AutonomousAgentRunStatus.Running or AutonomousAgentRunStatus.WaitingApproval,
                ct);
            if (!existingQueued)
            {
                db.AutonomousAcquisitionAgentRuns.Add(new AutonomousAcquisitionAgentRun
                {
                    TenantId = tenantId,
                    AgentId = agent.Id,
                    CampaignId = campaign.Id,
                    IsManual = true,
                    Status = AutonomousAgentRunStatus.Queued
                });
            }

            await db.SaveChangesAsync(ct);
            return Results.Accepted($"/api/autonomous-acquisition/tenants/{tenantId}/campaigns/{id}", new { campaign, agent });
        });

        g.MapPost("/tenants/{tenantId}/campaigns/{id}/approve", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var campaign = await db.Campaigns.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == id, ct);
            if (campaign is null) return Results.NotFound();

            var recipients = await db.CampaignRecipients
                .Where(x => x.TenantId == tenantId && x.CampaignId == id && x.Status == "pending-approval")
                .ToListAsync(ct);

            if (recipients.Count == 0)
                return Results.BadRequest(new { error = "There are no prepared recipients awaiting approval." });

            var prospectIds = recipients.Select(x => x.ProspectId).ToArray();
            var messages = await db.OutreachMessages
                .Where(x => x.TenantId == tenantId &&
                            x.CampaignId == id &&
                            prospectIds.Contains(x.ProspectId) &&
                            x.Status == OutreachStatus.Queued)
                .ToListAsync(ct);

            var approvalTitles = messages
                .Select(x => $"APPROVAL: Send outreach {x.Id}")
                .ToArray();

            var approvalTasks = await db.CrmTasks
                .Where(x => x.TenantId == tenantId &&
                            !x.Completed &&
                            approvalTitles.Contains(x.Title))
                .ToListAsync(ct);

            campaign.Status = CampaignStatus.Running;
            campaign.StartsAtUtc ??= DateTime.UtcNow;

            foreach (var recipient in recipients)
            {
                recipient.Status = "active";
                recipient.NextRunAtUtc = DateTime.UtcNow;
            }

            foreach (var approvalTask in approvalTasks)
                approvalTask.Completed = true;

            var agent = campaign.AgentId.HasValue
                ? await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(
                    x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value, ct)
                : null;

            if (agent is not null)
            {
                agent.Status = AutonomousAgentStatus.Active;
                agent.UpdatedAtUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);

            var waitingRun = agent is null
                ? null
                : await db.AutonomousAcquisitionAgentRuns
                    .Where(x => x.TenantId == tenantId &&
                                x.AgentId == agent.Id &&
                                x.Status == AutonomousAgentRunStatus.WaitingApproval)
                    .OrderByDescending(x => x.ScheduledAtUtc)
                    .FirstOrDefaultAsync(ct);

            if (waitingRun is not null)
            {
                waitingRun.Status = AutonomousAgentRunStatus.Queued;
                waitingRun.CompletedAtUtc = null;
                waitingRun.Error = null;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(new { campaign, approvedRecipients = recipients.Count, approvedMessages = messages.Count, completedApprovalTasks = approvalTasks.Count });
        });

        g.MapPost("/tenants/{tenantId}/campaigns/{id}/pause", async (Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
            if (campaign is null) return Results.NotFound();
            campaign.Pause();
            var runs = await db.AutonomousAcquisitionAgentRuns.Where(x => x.TenantId == tenantId && x.AgentId == campaign.AgentId && x.CampaignId == campaign.Id && x.Status == AutonomousAgentRunStatus.Running).ToListAsync(ct);
            foreach (var run in runs) run.Status = AutonomousAgentRunStatus.Paused;
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { campaign, pausedRuns = runs.Count });
        });

        g.MapPost("/tenants/{tenantId}/campaigns/{id}/resume", async (Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
            if (campaign is null) return Results.NotFound();
            campaign.Resume();
            var runs = await db.AutonomousAcquisitionAgentRuns.Where(x => x.TenantId == tenantId && x.AgentId == campaign.AgentId && x.Status == AutonomousAgentRunStatus.Paused).ToListAsync(ct);
            foreach (var run in runs) { run.Status = AutonomousAgentRunStatus.Queued; run.Error = null; }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { campaign, resumedRuns = runs.Count });
        });

        g.MapPost("/tenants/{tenantId}/campaigns/{id}/stop", async (Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
            if (campaign is null) return Results.NotFound();
            campaign.Stop();
            var runs = await db.AutonomousAcquisitionAgentRuns.Where(x => x.TenantId == tenantId && x.AgentId == campaign.AgentId && x.Status != AutonomousAgentRunStatus.Completed && x.Status != AutonomousAgentRunStatus.Cancelled).ToListAsync(ct);
            foreach (var run in runs) { run.Status = AutonomousAgentRunStatus.Cancelled; run.CompletedAtUtc = DateTime.UtcNow; }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { campaign, cancelledRuns = runs.Count });
        });

        g.MapGet("/tenants/{tenantId}/agents", async (
            Guid tenantId, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AutonomousAcquisitionAgents
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ToListAsync(ct)));

        g.MapGet("/tenants/{tenantId}/agents/{id}/tasks/{taskId}", async (
            Guid tenantId, Guid id, Guid taskId, AppDbContext db, CancellationToken ct) =>
        {
            var task = await db.AutonomousAcquisitionTasks.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.AgentId == id && x.Id == taskId, ct);
            if (task is null) return Results.NotFound();

            return Results.Ok(new
            {
                task.Id,
                task.AgentId,
                task.Sequence,
                task.Type,
                task.Name,
                task.Status,
                task.RequiresApproval,
                task.ConfigurationJson,
                task.ResultJson,
                task.Error,
                task.AttemptCount,
                task.StartedAtUtc,
                task.CompletedAtUtc,
                task.CreatedAtUtc,
                task.UpdatedAtUtc
            });
        });

        g.MapPost("/tenants/{tenantId}/agents/{id}/runs/{runId}/retry", async (
            Guid tenantId, Guid id, Guid runId, AppDbContext db, CancellationToken ct) =>
        {
            var run = await db.AutonomousAcquisitionAgentRuns.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.AgentId == id && x.Id == runId, ct);
            if (run is null) return Results.NotFound();
            if (run.Status != AutonomousAgentRunStatus.Failed)
                return Results.BadRequest(new { error = "Only failed runs can be retried." });

            run.Status = AutonomousAgentRunStatus.Queued;
            run.Error = null;
            run.StartedAtUtc = null;
            run.CompletedAtUtc = null;
            run.ScheduledAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Accepted(
                $"/api/autonomous-acquisition/tenants/{tenantId}/agents/{id}/runs/{run.Id}", run);
        });

        g.MapGet("/tenants/{tenantId}/agents/{id}/tasks", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AutonomousAcquisitionTasks
                .Where(x => x.TenantId == tenantId && x.AgentId == id)
                .OrderBy(x => x.Sequence)
                .ToListAsync(ct)));

        g.MapGet("/tenants/{tenantId}/agents/{id}/runs", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AutonomousAcquisitionAgentRuns
                .Where(x => x.TenantId == tenantId && x.AgentId == id)
                .OrderByDescending(x => x.ScheduledAtUtc)
                .Take(100)
                .ToListAsync(ct)));

        g.MapPost("/tenants/{tenantId}/agents/{id}/activate", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
            await SetStatus(tenantId, id, AutonomousAgentStatus.Active, db, ct));

        g.MapPost("/tenants/{tenantId}/agents/{id}/pause", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
            await SetStatus(tenantId, id, AutonomousAgentStatus.Paused, db, ct));

        g.MapPost("/tenants/{tenantId}/agents/{id}/stop", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
            await SetStatus(tenantId, id, AutonomousAgentStatus.Stopped, db, ct));

        g.MapPost("/tenants/{tenantId}/agents/{id}/run", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
        {
            var agent = await db.AutonomousAcquisitionAgents
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
            if (agent is null) return Results.NotFound();
            if (agent.Status is AutonomousAgentStatus.Stopped)
                return Results.BadRequest(new { error = "Agent is stopped." });
            if (agent.Status is AutonomousAgentStatus.Draft or AutonomousAgentStatus.Paused or AutonomousAgentStatus.Failed)
            {
                agent.Status = AutonomousAgentStatus.Active;
                agent.UpdatedAtUtc = DateTime.UtcNow;
            }

            var campaign = await db.Campaigns
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.AgentId == id, ct);
            if (campaign is null)
                return Results.BadRequest(new { error = "Agent is not linked to a campaign container." });
            if (campaign.Status is CampaignStatus.Stopped or CampaignStatus.Completed)
                return Results.BadRequest(new { error = $"Campaign is {campaign.Status}." });
            if (campaign.Status is CampaignStatus.Draft or CampaignStatus.Scheduled)
                campaign.Start();

            var run = new AutonomousAcquisitionAgentRun
            {
                TenantId = tenantId,
                AgentId = id,
                CampaignId = campaign.Id,
                IsManual = true,
                Status = AutonomousAgentRunStatus.Queued
            };

            db.AutonomousAcquisitionAgentRuns.Add(run);
            await db.SaveChangesAsync(ct);
            return Results.Accepted(
                $"/api/autonomous-acquisition/tenants/{tenantId}/agents/{id}/runs/{run.Id}", run);
        });

        return endpoints;
    }

    private static string ReadTaskPurpose(string json) => ReadTaskObjectProperty(json, "purpose");
    private static string ReadTaskNext(string json) => ReadTaskObjectProperty(json, "nextStep");
    private static object ReadTaskInput(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty("input", out var value) ? value.Clone() : new { };
        }
        catch { return new { }; }
    }
    private static string ReadTaskObjectProperty(string json, string property)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty(property, out var value) ? value.GetString() ?? string.Empty : string.Empty;
        }
        catch { return string.Empty; }
    }

    private static async Task<IResult> SetStatus(
        Guid tenantId, Guid id, AutonomousAgentStatus status, AppDbContext db, CancellationToken ct)
    {
        var agent = await db.AutonomousAcquisitionAgents
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (agent is null) return Results.NotFound();

        agent.Status = status;
        agent.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Results.Ok(agent);
    }

    public sealed record CreateCampaignRequest(
        string Name,
        string? PackageCode = null,
        string? Objective = null,
        string? Industry = null,
        string? Region = null,
        string[]? Countries = null,
        int MinimumScore = 0,
        int DailyDiscoveryLimit = 50,
        int DailyEmailLimit = 10,
        string? SenderName = null,
        string? SenderEmail = null,
        Dictionary<string, string>? Icp = null);
}