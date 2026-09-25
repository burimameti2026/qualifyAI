using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Infrastructure.Acquisition;
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

            var template = templates.Resolve(input.PackageCode ?? "custom");
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

            var target = new TargetList
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = input.Name + " targets",
                Description = template.TargetDefinition,
                Dynamic = true
            };

            var campaign = new Campaign
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                TargetListId = target.Id,
                AgentId = agent.Id,
                PackageCode = template.Code,
                Objective = input.Objective ?? template.Description,
                Name = input.Name,
                SenderName = input.SenderName ?? string.Empty,
                SenderEmail = input.SenderEmail ?? string.Empty,
                PlanStatus = "draft"
            };

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
                tasks = tasks.Select(t => new { t.Sequence, t.Type, t.Name, t.RequiresApproval }),
                messages = template.OutreachTemplates.Select(m => new { m.Step, m.Name, m.Subject, m.Body, m.DelayHours, m.RequiresApproval })
            });
            campaign.PlanStatus = "ready";
            await db.SaveChangesAsync(ct);

            return Results.Created(
                $"/api/autonomous-acquisition/tenants/{tenantId}/campaigns/{campaign.Id}",
                new { campaign, agent, package = template, tasks, target });
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

             IReadOnlyList<AutonomousAcquisitionTask> tasks = agent is null
                ? Array.Empty<AutonomousAcquisitionTask>()
                : await db.AutonomousAcquisitionTasks
                    .Where(x => x.TenantId == tenantId && x.AgentId == agent.Id)
                    .OrderBy(x => x.Sequence)
                    .ToListAsync(ct);

            var steps = await db.CampaignSteps
                .Where(x => x.TenantId == tenantId && x.CampaignId == campaign.Id)
                .OrderBy(x => x.StepNumber)
                .ToListAsync(ct);

            return Results.Ok(new { campaign, agent, tasks, steps });
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

            campaign.Status = CampaignStatus.Running;
            campaign.StartsAtUtc ??= DateTime.UtcNow;

            foreach (var recipient in recipients)
            {
                recipient.Status = "active";
                recipient.NextRunAtUtc = DateTime.UtcNow;
            }

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
                waitingRun.Status = AutonomousAgentRunStatus.Completed;
                waitingRun.CompletedAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
            }

            return Results.Ok(new { campaign, approvedRecipients = recipients.Count });
        });

        g.MapGet("/tenants/{tenantId}/agents", async (
            Guid tenantId, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AutonomousAcquisitionAgents
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .ToListAsync(ct)));

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

            var run = new AutonomousAcquisitionAgentRun
            {
                TenantId = tenantId,
                AgentId = id,
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