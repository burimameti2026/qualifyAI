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


}
