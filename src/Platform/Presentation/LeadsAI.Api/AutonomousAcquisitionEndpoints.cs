using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Persistence.SqlServer;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.BuildingBlocks.Security.Access;

namespace LeadsAI.Api;

public static class AutonomousAcquisitionEndpoints
{
    public static IEndpointRouteBuilder MapAutonomousAcquisition(this IEndpointRouteBuilder endpoints)
    {
        var g = endpoints.MapGroup("/api/autonomous-acquisition")
            .RequireAuthorization("qai:module:" + QualifyAiModules.Crm);

        g.AddEndpointFilter((ctx, next) =>
        {
            if (ctx.HttpContext.Request.RouteValues.TryGetValue("tenantId", out var raw) &&
                Guid.TryParse(raw?.ToString(), out var routeTenant))
            {
                var current = ctx.HttpContext.RequestServices
                    .GetRequiredService<ITenantContext>()
                    .TenantId();

                if (routeTenant != current)
                    return ValueTask.FromResult<object?>(Results.Forbid());
            }

            return next(ctx);
        });

        g.MapPost("/campaigns/{campaignId}/run", async (Guid campaignId, AppDbContext db, ITenantContext tenantContext, CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current?.Id ?? Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Forbid();

            var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == campaignId, ct);
            if (campaign is null) return Results.NotFound();
            if (!campaign.AgentId.HasValue) return Results.BadRequest(new { error = "Campaign has no autonomous acquisition agent." });
            if (campaign.Status != CampaignStatus.Running) return Results.BadRequest(new { error = "Campaign must be running before an acquisition run can start." });

            var agent = await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value, ct);
            if (agent is null) return Results.BadRequest(new { error = "Campaign agent was not found." });
            if (agent.Status != AutonomousAgentStatus.Active) return Results.BadRequest(new { error = "Autonomous acquisition agent must be active." });

            var duplicate = await db.AutonomousAcquisitionAgentRuns.AnyAsync(x =>
                x.TenantId == tenantId && x.AgentId == agent.Id && x.CampaignId == campaign.Id &&
                (x.Status == AutonomousAgentRunStatus.Queued || x.Status == AutonomousAgentRunStatus.Running || x.Status == AutonomousAgentRunStatus.WaitingApproval), ct);
            if (duplicate) return Results.Conflict(new { error = "An acquisition run is already queued, running, or waiting for approval." });

            var run = new AutonomousAcquisitionAgentRun
            {
                TenantId = tenantId, AgentId = agent.Id, CampaignId = campaign.Id,
                IsManual = true, Status = AutonomousAgentRunStatus.Queued, ScheduledAtUtc = DateTime.UtcNow
            };
            db.AutonomousAcquisitionAgentRuns.Add(run);
            await db.SaveChangesAsync(ct);
            return Results.Accepted($"/api/autonomous-acquisition/campaigns/{campaign.Id}/runs/{run.Id}", run);
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
                currentStep = tasks.FirstOrDefault(x => (x.Status == AutonomousAgentTaskStatus.Running || x.Status == AutonomousAgentTaskStatus.Pending)),
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



        g.MapGet("/tenants/{tenantId}/agents/{id}/tasks", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AutonomousAcquisitionTasks
                .Where(x => x.TenantId == tenantId && x.AgentId == id)
                .OrderBy(x => x.Sequence)
                .ToListAsync(ct)));

        g.MapPost("/tenants/{tenantId}/agents/{id}/runs", async (
            Guid tenantId, Guid id, RunRequest input, AppDbContext db, ITenantContext tenant, CancellationToken ct) =>
        {
            if (tenantId != tenant.TenantId())
                return Results.Forbid();

            var campaign = await db.Campaigns.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == input.CampaignId, ct);
            if (campaign is null) return Results.NotFound(new { detail = "Campaign was not found." });
            if ((campaign.Status == CampaignStatus.Paused || campaign.Status == CampaignStatus.Stopped || campaign.Status == CampaignStatus.Completed))
                return Results.Conflict(new { detail = "The campaign is not runnable in its current status." });

            var agent = await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == id, ct);
            if (agent is null) return Results.NotFound(new { detail = "Agent was not found." });
            if ((agent.Status == AutonomousAgentStatus.Stopped))
                return Results.Conflict(new { detail = "The autonomous acquisition agent is stopped." });

            var active = await db.AutonomousAcquisitionAgentRuns.AnyAsync(
                x => x.TenantId == tenantId && x.AgentId == id && x.CampaignId == input.CampaignId &&
                     (x.Status == AutonomousAgentRunStatus.Queued || x.Status == AutonomousAgentRunStatus.Running || x.Status == AutonomousAgentRunStatus.WaitingApproval), ct);
            if (active) return Results.Conflict(new { detail = "An acquisition run is already active for this campaign." });

            var run = new AutonomousAcquisitionAgentRun
            {
                TenantId = tenantId,
                AgentId = id,
                CampaignId = input.CampaignId,
                IsManual = true,
                Status = AutonomousAgentRunStatus.Queued,
                ScheduledAtUtc = DateTime.UtcNow
            };
            db.AutonomousAcquisitionAgentRuns.Add(run);
            await db.SaveChangesAsync(ct);
            return Results.Ok(run);
        })
        .RequireAuthorization("qai:module:" + QualifyAiModules.Crm)
        .AddEndpointFilter(async (ctx, next) =>
        {
            var user = ctx.HttpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
            return await next(ctx);
        });

                g.MapGet("/tenants/{tenantId}/agents/{id}/runs", async (
            Guid tenantId, Guid id, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.AutonomousAcquisitionAgentRuns
                .Where(x => x.TenantId == tenantId && x.AgentId == id)
                .OrderByDescending(x => x.ScheduledAtUtc)
                .Take(100)
                .ToListAsync(ct)));









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

    private sealed record RunRequest(Guid CampaignId);
}
