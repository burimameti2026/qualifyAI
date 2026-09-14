using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Infrastructure.Automation;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api;

public static class AutomationAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAutomationAnalytics(this IEndpointRouteBuilder app)
    {
        var automation = app.MapGroup("/api/automation").RequireAuthorization();
        var analytics = app.MapGroup("/api/analytics").RequireAuthorization();

        automation.MapGet("/rules", async (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
            Results.Ok(await db.AutomationRules.AsNoTracking().Where(x => x.TenantId == tenant.Id()).OrderBy(x => x.Name).ToListAsync(ct)));

        automation.MapPost("/rules", async (AutomationRuleRequest request, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
        {
            try
            {
                var rule = AutomationRule.Create(tenant.Id(), request.Name, request.Trigger, request.ConditionsJson ?? "[]", request.ActionsJson ?? "[]", request.Active);
                db.AutomationRules.Add(rule);
                await db.SaveChangesAsync(ct);
                return Results.Created($"/api/automation/rules/{rule.Id}", rule);
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        automation.MapPut("/rules/{id:guid}", async (Guid id, AutomationRuleRequest request, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
        {
            var rule = await db.AutomationRules.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id(), ct);
            if (rule is null) return Results.NotFound();
            try
            {
                rule.UpdateConfiguration(request.Name, request.Trigger, request.ConditionsJson ?? "[]", request.ActionsJson ?? "[]", request.Active);
                await db.SaveChangesAsync(ct);
                return Results.Ok(rule);
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
        });

        automation.MapPost("/rules/{id:guid}/run", async (Guid id, AutomationRunRequest? request, AppDbContext db, ICurrentTenant tenant, AutomationActionExecutor executor, CancellationToken ct) =>
        {
            var rule = await db.AutomationRules.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id() && x.Active, ct);
            if (rule is null) return Results.NotFound();
            var triggerData = request?.TriggerDataJson ?? "{\"source\":\"manual\"}";
            try { WorkflowNode.EnsureJson(triggerData, "Automation trigger data"); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
            var run = AutomationRun.Create(tenant.Id(), rule.Id, triggerData);
            db.AutomationRuns.Add(run); run.Start(); await db.SaveChangesAsync(ct);
            var result = await executor.ExecuteAsync(rule, run, ct);
            if (result.Success) run.Complete(result.LogJson); else run.Fail(result.LogJson);
            await db.SaveChangesAsync(ct);
            return result.Success ? Results.Ok(run) : Results.Problem(result.Error ?? "Automation execution failed", statusCode: 422);
        });

        automation.MapGet("/runs/{id:guid}", async (Guid id, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
        {
            var run = await db.AutomationRuns.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id(), ct);
            return run is null ? Results.NotFound() : Results.Ok(run);
        });

        analytics.MapGet("/overview", async (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
        {
            var tenantId = tenant.Id();
            var prospects = db.Prospects.Where(x => x.TenantId == tenantId);
            var campaigns = db.Campaigns.Where(x => x.TenantId == tenantId);
            var outreach = db.OutreachMessages.Where(x => x.TenantId == tenantId);
            var opportunities = db.Opportunitys.Where(x => x.TenantId == tenantId);
            var runs = db.AutomationRuns.Where(x => x.TenantId == tenantId);
            var revenue = await opportunities.Where(x => x.Status == OpportunityStatus.Won).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            var pipeline = await opportunities.Where(x => x.Status == OpportunityStatus.Open).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
            var sent = await outreach.CountAsync(x => x.Status == OutreachStatus.Sent || x.Status == OutreachStatus.Delivered || x.Status == OutreachStatus.Replied, ct);
            var replied = await outreach.CountAsync(x => x.Status == OutreachStatus.Replied, ct);
            return Results.Ok(new
            {
                generatedAtUtc = DateTime.UtcNow,
                prospects = await prospects.CountAsync(ct),
                qualifiedProspects = await prospects.CountAsync(x => x.Status == ProspectStatus.Qualified, ct),
                targetListMembers = await db.TargetListMembers.CountAsync(x => x.TenantId == tenantId, ct),
                campaigns = await campaigns.CountAsync(ct),
                activeCampaigns = await campaigns.CountAsync(x => x.Status == CampaignStatus.Running, ct),
                outreachSent = sent,
                replies = replied,
                replyRate = sent == 0 ? 0m : Math.Round((decimal)replied / sent * 100m, 2),
                openPipeline = pipeline,
                wonRevenue = revenue,
                automationRuns = await runs.CountAsync(ct),
                automationCompleted = await runs.CountAsync(x => x.Status == "completed", ct),
                automationFailed = await runs.CountAsync(x => x.Status == "failed", ct)
            });
        });

        analytics.MapGet("/metrics", async (DateTime? fromUtc, DateTime? toUtc, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
        {
            var tenantId = tenant.Id();
            var from = fromUtc ?? DateTime.UtcNow.Date.AddDays(-30);
            var to = toUtc ?? DateTime.UtcNow;
            var metrics = await db.MetricSnapshots.AsNoTracking().Where(x => x.TenantId == tenantId && x.PeriodEndUtc >= from && x.PeriodStartUtc <= to).OrderByDescending(x => x.PeriodStartUtc).Take(500).ToListAsync(ct);
            return Results.Ok(new { fromUtc = from, toUtc = to, metrics });
        });

        analytics.MapPost("/metrics/snapshot", async (MetricSnapshotRequest request, AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(request.Metric)) return Results.BadRequest(new { error = "metric is required" });
            var snapshot = new MetricSnapshot { Id = Guid.NewGuid(), TenantId = tenant.Id(), Metric = request.Metric.Trim().ToLowerInvariant(), Value = request.Value, PeriodStartUtc = request.PeriodStartUtc, PeriodEndUtc = request.PeriodEndUtc, DimensionsJson = request.DimensionsJson ?? "{}" };
            try { WorkflowNode.EnsureJson(snapshot.DimensionsJson, "Metric dimensions"); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
            db.MetricSnapshots.Add(snapshot); await db.SaveChangesAsync(ct); return Results.Created($"/api/analytics/metrics/{snapshot.Id}", snapshot);
        });

        analytics.MapGet("/attribution", async (AppDbContext db, ICurrentTenant tenant, CancellationToken ct) =>
        {
            var rows = await db.RevenueAttributions.AsNoTracking().Where(x => x.TenantId == tenant.Id()).OrderByDescending(x => x.CreatedAtUtc).Take(500).ToListAsync(ct);
            return Results.Ok(new { totalInfluencedRevenue = rows.Sum(x => x.InfluencedRevenue), count = rows.Count, items = rows });
        });

        return app;
    }

    public sealed record AutomationRuleRequest(string Name, string Trigger, string? ConditionsJson, string? ActionsJson, bool Active = true);
    public sealed record AutomationRunRequest(string? TriggerDataJson);
    public sealed record MetricSnapshotRequest(string Metric, decimal Value, DateTime PeriodStartUtc, DateTime PeriodEndUtc, string? DimensionsJson);
}
