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
                     (x.Status == AutonomousAgentRunStatus.Queued ||
                      x.Status == AutonomousAgentRunStatus.Running ||
                      x.Status == AutonomousAgentRunStatus.WaitingApproval),
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
