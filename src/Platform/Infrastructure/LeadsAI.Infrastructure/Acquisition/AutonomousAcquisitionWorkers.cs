using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LeadsAI.Application;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

public sealed class AutonomousAcquisitionQueuedRunWorker(IServiceScopeFactory scopes, ILogger<AutonomousAcquisitionQueuedRunWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var rootScope = scopes.CreateAsyncScope();
                var services = rootScope.ServiceProvider;
                var runtime = services.GetRequiredService<TenantWorkerRuntime>();
                var tenants = await runtime.ActiveCampaignTenantsAsync(stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ProcessTenantDatabaseAsync(services, tenant.Id, tenant.Slug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { log.LogError(ex, "Autonomous acquisition queue worker iteration failed"); }
        }
    }

    private async Task ProcessTenantDatabaseAsync(IServiceProvider rootServices, Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var ids = await db.AutonomousAcquisitionAgentRuns
            .Where(x => x.TenantId == tenantId &&
                        x.Status == AutonomousAgentRunStatus.Queued &&
                        db.Campaigns.Any(campaign =>
                            campaign.TenantId == tenantId &&
                            campaign.Id == x.CampaignId &&
                            campaign.Status == CampaignStatus.Running &&
                            campaign.AgentId == x.AgentId) &&
                        db.AutonomousAcquisitionAgents.Any(agent =>
                            agent.TenantId == tenantId &&
                            agent.Id == x.AgentId &&
                            agent.Status == AutonomousAgentStatus.Active))
            .OrderBy(x => x.ScheduledAtUtc)
            .Take(10)
            .Select(x => x.Id)
            .ToListAsync(ct);

        await Parallel.ForEachAsync(
            ids,
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (id, token) =>
            {
                await using var runScope = rootServices.CreateAsyncScope();
                var runTenantContext = runScope.ServiceProvider.GetRequiredService<ITenantContext>();
                runTenantContext.Set(new CurrentTenant(tenantId, tenantSlug));

                try
                {
                    var orchestrator = runScope.ServiceProvider.GetRequiredService<IAutonomousAcquisitionRunOrchestrator>();
                    await orchestrator.ExecuteAsync(id, token);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Autonomous acquisition run {RunId} failed", id);
                }
            });
    }
}

public sealed class AutonomousAcquisitionSchedulerWorker(IServiceScopeFactory scopes, ILogger<AutonomousAcquisitionSchedulerWorker> log) : BackgroundService
{
    private const string TimeZoneSetting = "acquisition.schedule.timeZoneId";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var rootScope = scopes.CreateAsyncScope();
                var services = rootScope.ServiceProvider;
                var runtime = services.GetRequiredService<TenantWorkerRuntime>();
                var tenants = await runtime.ActiveCampaignTenantsAsync(stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ScheduleTenantDatabaseAsync(services, tenant.Id, tenant.Slug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { log.LogError(ex, "Autonomous acquisition scheduler iteration failed"); }
        }
    }

    private async Task ScheduleTenantDatabaseAsync(IServiceProvider rootServices, Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));
        await ScheduleScopeAsync(scope.ServiceProvider, ct);
    }

    private static async Task ScheduleScopeAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var tenantId = services.GetRequiredService<ITenantContext>().Current?.Id
            ?? throw new InvalidOperationException("Tenant context is not set.");

        var agents = await db.AutonomousAcquisitionAgents
            .Where(x => x.TenantId == tenantId &&
                        x.Status == AutonomousAgentStatus.Active &&
                        db.Campaigns.Any(c => c.TenantId == tenantId &&
                                               c.AgentId == x.Id &&
                                               c.Status == CampaignStatus.Running))
            .ToListAsync(ct);

        if (agents.Count == 0) return;

        var timeZoneId = await db.TenantSettings
            .Where(x => x.TenantId == tenantId && x.Key == TimeZoneSetting)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);

        var timeZone = ResolveTimeZone(timeZoneId);
        var pendingAgentIds = await db.AutonomousAcquisitionAgentRuns
            .Where(x => x.TenantId == tenantId &&
                        (x.Status == AutonomousAgentRunStatus.Queued ||
                         x.Status == AutonomousAgentRunStatus.Running ||
                         x.Status == AutonomousAgentRunStatus.WaitingApproval))
            .Select(x => x.AgentId)
            .Distinct()
            .ToListAsync(ct);

        var pending = pendingAgentIds.ToHashSet();
        var nowUtc = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var localToday = DateOnly.FromDateTime(localNow);
        var changed = false;

        foreach (var agent in agents)
        {
            if (pending.Contains(agent.Id)) continue;
            var campaign = await db.Campaigns
                .Where(x => x.TenantId == tenantId && x.AgentId == agent.Id && x.Status == CampaignStatus.Running)
                .OrderByDescending(x => x.UpdatedAtUtc)
                .FirstOrDefaultAsync(ct);
            if (campaign is null) continue;

            var due = localNow.TimeOfDay >= agent.RunTimeUtc.ToTimeSpan();
            var already = agent.LastRunAtUtc.HasValue &&
                          DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agent.LastRunAtUtc.Value, timeZone)) == localToday;

            if (!due || already) continue;

            db.AutonomousAcquisitionAgentRuns.Add(new AutonomousAcquisitionAgentRun
            {
                TenantId = tenantId,
                AgentId = agent.Id,
                CampaignId = campaign.Id,
                IsManual = false,
                Status = AutonomousAgentRunStatus.Queued,
                ScheduledAtUtc = nowUtc
            });

            pending.Add(agent.Id);
            agent.LastRunAtUtc = nowUtc;
            agent.UpdatedAtUtc = nowUtc;
            changed = true;
        }

        if (changed) await db.SaveChangesAsync(ct);
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }

        return TimeZoneInfo.Utc;
    }
}
