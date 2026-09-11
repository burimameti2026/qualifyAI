using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Tenancy;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public static class RealWorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapRealWorkspace(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/real-workspace");

        g.MapGet("/options", (IAutonomousAcquisitionTemplateRegistry templates) => Results.Ok(new
        {
            useCases = new[]
            {
                new { id = "autonomous-acquisition", name = "Autonomous Acquisition", description = "Automatically discover, enrich, qualify and route prospects for this tenant." }
            },
            templates = templates.List().Select(t => new
            {
                id = t.Code,
                name = t.Name,
                useCaseId = "autonomous-acquisition",
                description = $"{t.Industry} prospecting in {t.Region}.",
                requiredModules = new[] { "acquisition", "automation" }
            }),
            schedule = "daily"
        }));

        g.MapPost("/prepare", async (RealWorkspaceRequest request, ICurrentTenant currentTenant, AppDbContext db, IAutonomousAcquisitionTemplateRegistry templates, CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(request, currentTenant);
            if (tenantId is null) return Results.BadRequest(new { error = "The selected workspace does not belong to the current tenant." });

            var scopedRequest = request with { TenantId = tenantId.Value };
            var agent = await EnsureAgent(scopedRequest, db, templates, ct);
            var workspace = await EnsureCampaignWorkspace(scopedRequest, agent, db, ct);

            agent.Status = AutonomousAgentStatus.Active;
            agent.UpdatedAtUtc = DateTime.UtcNow;

            var existingQueuedOrRunning = await db.AutonomousAcquisitionAgentRuns.AnyAsync(x =>
                x.TenantId == tenantId.Value &&
                x.AgentId == agent.Id &&
                (x.Status == AutonomousAgentRunStatus.Queued || x.Status == AutonomousAgentRunStatus.Running), ct);

            AutonomousAcquisitionAgentRun? initialRun = null;
            if (!existingQueuedOrRunning)
            {
                initialRun = new AutonomousAcquisitionAgentRun
                {
                    TenantId = tenantId.Value,
                    AgentId = agent.Id,
                    IsManual = true,
                    Status = AutonomousAgentRunStatus.Queued,
                    ScheduledAtUtc = DateTime.UtcNow
                };
                db.AutonomousAcquisitionAgentRuns.Add(initialRun);
            }

            await db.SaveChangesAsync(ct);
            return Results.Accepted($"/api/real-workspace/tenants/{tenantId.Value}",
                new RealWorkspaceResult(tenantId.Value, agent.Id, agent.Name, agent.Status.ToString(), initialRun?.Id,
                    existingQueuedOrRunning ? "already-queued" : "activation-queued", workspace.TargetList.Id, workspace.Campaign.Id));
        });

        g.MapPost("/activate", async (RealWorkspaceRequest request, ICurrentTenant currentTenant, AppDbContext db, IAutonomousAcquisitionTemplateRegistry templates, CancellationToken ct) =>
        {
            var tenantId = ResolveTenant(request, currentTenant);
            if (tenantId is null) return Results.BadRequest(new { error = "The selected workspace does not belong to the current tenant." });
            var scopedRequest = request with { TenantId = tenantId.Value };

            var agent = await EnsureAgent(scopedRequest, db, templates, ct);
            var workspace = await EnsureCampaignWorkspace(scopedRequest, agent, db, ct);
            agent.Status = AutonomousAgentStatus.Active;
            agent.UpdatedAtUtc = DateTime.UtcNow;

            var existingQueuedOrRunning = await db.AutonomousAcquisitionAgentRuns.AnyAsync(x => x.TenantId == tenantId.Value && x.AgentId == agent.Id && (x.Status == AutonomousAgentRunStatus.Queued || x.Status == AutonomousAgentRunStatus.Running), ct);
            AutonomousAcquisitionAgentRun? initialRun = null;
            if (!existingQueuedOrRunning)
            {
                initialRun = new AutonomousAcquisitionAgentRun
                {
                    TenantId = tenantId.Value,
                    AgentId = agent.Id,
                    IsManual = true,
                    Status = AutonomousAgentRunStatus.Queued,
                    ScheduledAtUtc = DateTime.UtcNow
                };
                db.AutonomousAcquisitionAgentRuns.Add(initialRun);
            }

            await db.SaveChangesAsync(ct);
            return Results.Accepted($"/api/real-workspace/tenants/{tenantId.Value}", new RealWorkspaceResult(tenantId.Value, agent.Id, agent.Name, agent.Status.ToString(), initialRun?.Id, "activation-queued", workspace.TargetList.Id, workspace.Campaign.Id));
        });

        return app;
    }

    private static Guid? ResolveTenant(RealWorkspaceRequest request, ICurrentTenant currentTenant)
    {
        if (!currentTenant.IsResolved) return null;
        if (request.TenantId != Guid.Empty && request.TenantId != currentTenant.Id) return null;
        return currentTenant.Id;
    }

    private static async Task<(TargetList TargetList, Campaign Campaign)> EnsureCampaignWorkspace(RealWorkspaceRequest request, AutonomousAcquisitionAgent agent, AppDbContext db, CancellationToken ct)
    {
        var workspaceName = string.IsNullOrWhiteSpace(request.Name) ? agent.Name : request.Name.Trim();
        var targetListName = $"{workspaceName} — Qualified Prospects";
        var targetList = await db.TargetLists.FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.Name == targetListName, ct);
        if (targetList is null)
        {
            targetList = new TargetList { Id = Guid.NewGuid(), TenantId = request.TenantId, Name = targetListName, Description = "Qualified prospects produced by the autonomous acquisition agent.", Dynamic = true };
            db.TargetLists.Add(targetList);
        }

        var campaignName = $"{workspaceName} — Acquisition Campaign";
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.Name == campaignName, ct);
        if (campaign is null)
        {
            campaign = new Campaign
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                TargetListId = targetList.Id,
                Name = campaignName,
                Goal = "book-demo",
                SenderName = string.Empty,
                SenderEmail = string.Empty
            };
            db.Campaigns.Add(campaign);
        }
        else
        {
            campaign.TargetListId = targetList.Id;
        }

        return (targetList, campaign);
    }

    private static async Task<AutonomousAcquisitionAgent> EnsureAgent(RealWorkspaceRequest request, AppDbContext db, IAutonomousAcquisitionTemplateRegistry templates, CancellationToken ct)
    {
        var requestedName = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim();
        var defaultName = string.Equals(request.TemplateKey, "fleet", StringComparison.OrdinalIgnoreCase)
            ? "FusionFleet Autonomous Acquisition"
            : "Real Workspace Acquisition";
        var agentName = requestedName ?? defaultName;
        var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.Name == agentName, ct);

        if (agent is null)
        {
            agent = new AutonomousAcquisitionAgent
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                Name = agentName,
                TemplateCode = request.TemplateKey ?? "custom",
                Industry = request.Industry ?? string.Empty,
                Region = request.Region ?? string.Empty,
                CountriesJson = request.CountriesJson ?? "[]",
                DailyDiscoveryLimit = request.DailyDiscoveryLimit > 0 ? request.DailyDiscoveryLimit : 25,
                MinimumScore = request.MinimumScore > 0 ? request.MinimumScore : 70,
                RunTimeUtc = request.RunTimeUtc ?? new TimeOnly(8, 0),
                Status = AutonomousAgentStatus.Draft,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            templates.Apply(agent);
            db.AutonomousAcquisitionAgents.Add(agent);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.TemplateKey)) agent.TemplateCode = request.TemplateKey;
            if (!string.IsNullOrWhiteSpace(request.Industry)) agent.Industry = request.Industry;
            if (!string.IsNullOrWhiteSpace(request.Region)) agent.Region = request.Region;
            if (!string.IsNullOrWhiteSpace(request.CountriesJson)) agent.CountriesJson = request.CountriesJson;
            if (request.DailyDiscoveryLimit > 0) agent.DailyDiscoveryLimit = request.DailyDiscoveryLimit;
            if (request.MinimumScore > 0) agent.MinimumScore = request.MinimumScore;
            if (request.RunTimeUtc.HasValue) agent.RunTimeUtc = request.RunTimeUtc.Value;
            agent.UpdatedAtUtc = DateTime.UtcNow;
            templates.Apply(agent);
        }

        await db.SaveChangesAsync(ct);
        return agent;
    }
}

public sealed record RealWorkspaceRequest(Guid TenantId, string? Name, string? UseCase, string? TemplateKey, string? Industry, string? Region, string? CountriesJson, int DailyDiscoveryLimit = 25, int MinimumScore = 70, TimeOnly? RunTimeUtc = null);
public sealed record RealWorkspaceResult(Guid TenantId, Guid AgentId, string AgentName, string AgentStatus, Guid? InitialRunId, string Status, Guid? TargetListId, Guid? CampaignId);
