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

        g.MapPost("/campaigns/{campaignId}/run", async (
            Guid campaignId,
            AppDbContext db,
            ITenantContext tenantContext,
            IAgentJobFactory jobFactory,
            ICampaignContainerRuntime containers,
            CancellationToken ct) =>
        {
            var tenantId = tenantContext.Current?.Id ?? Guid.Empty;
            if (tenantId == Guid.Empty) return Results.Forbid();

            var campaign = await db.Campaigns.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == campaignId, ct);
            if (campaign is null) return Results.NotFound();
            if (!campaign.AgentId.HasValue)
                return Results.BadRequest(new { error = "Campaign has no autonomous acquisition agent." });
            if (campaign.Status != CampaignStatus.Running)
                return Results.BadRequest(new { error = "Campaign must be running before an acquisition job can start." });

            var agent = await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value, ct);
            if (agent is null)
                return Results.BadRequest(new { error = "Campaign agent was not found." });
            if (agent.Status != AutonomousAgentStatus.Active)
                return Results.BadRequest(new { error = "Autonomous acquisition agent must be active." });

            var container = await containers.EnsureAsync(
                tenantId,
                campaign.Id,
                agent.Id,
                $"{campaign.Name} Container",
                campaign.PackageCode,
                campaign.PackageVersion,
                campaign.PlanJson,
                ct);

            var job = await jobFactory.QueueCampaignAsync(
                tenantId,
                campaign.Id,
                agent.Id,
                container.Id,
                "campaign.execute",
                $"campaign:{campaign.Id}",
                true,
                ct);

            return Results.Accepted(
                $"/api/autonomous-acquisition/tenants/{tenantId}/jobs/{job.Id}",
                new { job, container });
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
                : await db.AgentJobs
                    .AsNoTracking()
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

            var container = await db.CampaignContainers
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.CampaignId == campaign.Id)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync(ct);

            return Results.Ok(new
            {
                campaign,
                latestRun,
                container,
                runtime = new
                {
                    status = latestRun?.Status.ToString() ?? container?.Status.ToString() ?? "Unknown",
                    containerStatus = container?.Status.ToString() ?? "Unknown",
                    containerVersion = container?.Version,
                    containerVersionLabel = container?.VersionLabel,
                    jobId = latestRun?.Id,
                    jobStatus = latestRun?.Status.ToString(),
                    jobType = latestRun?.Type,
                    taskCount = tasks.Count,
                    completedTasks = tasks.Count(x => x.Status == AutonomousAgentTaskStatus.Completed),
                    runningTask = tasks.FirstOrDefault(x => x.Status == AutonomousAgentTaskStatus.Running)?.Id
                },
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
                        statusCode = t.Status.ToString(),
                        status = t.Status switch
                        {
                            AutonomousAgentTaskStatus.Pending => "IDLE",
                            AutonomousAgentTaskStatus.Running => "RUNNING",
                            AutonomousAgentTaskStatus.Completed => "DONE",
                            AutonomousAgentTaskStatus.Paused => "PAUSED",
                            AutonomousAgentTaskStatus.Failed => "FAILED",
                            AutonomousAgentTaskStatus.Skipped => "SKIPPED",
                            _ => "UNKNOWN"
                        },
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






        g.MapGet("/tenants/{tenantId}/campaigns/{campaignId}/container", async (
            Guid tenantId, Guid campaignId, AppDbContext db, CancellationToken ct) =>
        {
            var container = await db.CampaignContainers
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.CampaignId == campaignId)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync(ct);

            return container is null ? Results.NotFound() : Results.Ok(container);
        });

        g.MapGet("/tenants/{tenantId}/containers", async (
            Guid tenantId, AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.CampaignContainers
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Take(100)
                .ToListAsync(ct)));

        g.MapPost("/tenants/{tenantId}/campaigns/{campaignId}/container/stop", async (
            Guid tenantId, Guid campaignId, AppDbContext db, ICampaignContainerRuntime containers, CancellationToken ct) =>
        {
            var container = await db.CampaignContainers
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CampaignId == campaignId, ct);

            if (container is null) return Results.NotFound(new { error = "campaign_container_not_found" });

            containers.Stop(container, DateTime.UtcNow);
            await db.SaveChangesAsync(ct);

            return Results.Ok(container);
        });

        g.MapPost("/tenants/{tenantId}/campaigns/{campaignId}/container/queue", async (
            Guid tenantId, Guid campaignId, AppDbContext db, ICampaignContainerRuntime containers, CancellationToken ct) =>
        {
            var container = await db.CampaignContainers
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.CampaignId == campaignId, ct);

            if (container is null) return Results.NotFound(new { error = "campaign_container_not_found" });

            if (container.Status is CampaignContainerStatus.Running)
                return Results.Conflict(new { error = "campaign_container_already_running" });

            containers.Queue(container);
            await db.SaveChangesAsync(ct);

            return Results.Accepted($"/api/autonomous-acquisition/tenants/{tenantId}/campaigns/{campaignId}/container", container);
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
            Guid tenantId,
            Guid id,
            RunRequest input,
            AppDbContext db,
            IAgentJobFactory jobFactory,
            ICampaignContainerRuntime containers,
            ITenantContext tenant,
            CancellationToken ct) =>
        {
            if (tenantId != tenant.TenantId())
                return Results.Forbid();

            var campaign = await db.Campaigns.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == input.CampaignId, ct);
            if (campaign is null) return Results.NotFound(new { detail = "Campaign was not found." });
            if (campaign.Status is CampaignStatus.Paused or CampaignStatus.Stopped or CampaignStatus.Completed)
                return Results.Conflict(new { detail = "The campaign is not runnable in its current status." });

            var agent = await db.AutonomousAcquisitionAgents.SingleOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == id, ct);
            if (agent is null) return Results.NotFound(new { detail = "Agent was not found." });
            if (agent.Status == AutonomousAgentStatus.Stopped)
                return Results.Conflict(new { detail = "The autonomous acquisition agent is stopped." });

            var container = await containers.EnsureAsync(
                tenantId,
                campaign.Id,
                agent.Id,
                $"{campaign.Name} Container",
                campaign.PackageCode,
                campaign.PackageVersion,
                campaign.PlanJson,
                ct);

            var job = await jobFactory.QueueCampaignAsync(
                tenantId,
                campaign.Id,
                agent.Id,
                container.Id,
                "campaign.execute",
                $"campaign:{campaign.Id}",
                true,
                ct);

            return Results.Accepted(
                $"/api/autonomous-acquisition/tenants/{tenantId}/jobs/{job.Id}",
                new { job, container });
        })
        .RequireAuthorization("qai:module:" + QualifyAiModules.Crm)
        .AddEndpointFilter(async (ctx, next) =>
        {
            var user = ctx.HttpContext.User;
            if (!user.Identity?.IsAuthenticated ?? true) return Results.Unauthorized();
            return await next(ctx);
        });

        g.MapGet("/tenants/{tenantId}/agents/{id}/runs", async (
            Guid tenantId,
            Guid id,
            AppDbContext db,
            CancellationToken ct) =>
            Results.Ok(await db.AgentJobs
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.AgentId == id)
                .OrderByDescending(x => x.ScheduledAtUtc)
                .Take(100)
                .Select(x => new
                {
                    x.Id,
                    x.TenantId,
                    x.AgentId,
                    x.CampaignId,
                    x.ContainerId,
                    x.ContainerVersion,
                    x.TaskId,
                    x.TaskType,
                    x.Status,
                    x.IsManual,
                    x.Query,
                    x.DiscoveredCount,
                    x.QualifiedCount,
                    x.HighScoreCount,
                    x.EmailsQueuedCount,
                    x.EmailsSentCount,
                    x.ScheduledAtUtc,
                    x.StartedAtUtc,
                    x.CompletedAtUtc,
                    x.Error,
                    x.AttemptCount,
                    x.CreatedAtUtc,
                    x.UpdatedAtUtc
                })
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
