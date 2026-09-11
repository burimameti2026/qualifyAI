using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QualifyAI.Application;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

public sealed class AutonomousAcquisitionQueuedRunWorker(IServiceScopeFactory scopes, ILogger<AutonomousAcquisitionQueuedRunWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDefaultDatabaseAsync(stoppingToken);
                using var rootScope = scopes.CreateScope();
                var resolver = rootScope.ServiceProvider.GetRequiredService<ITenantDatabaseConnectionResolver>();
                foreach (var tenantSlug in resolver.GetConfiguredTenantDatabases().Keys)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    var tenantId = await FindTenantIdAsync(tenantSlug, stoppingToken);
                    if (tenantId is null) continue;
                    await ProcessTenantDatabaseAsync(tenantId.Value, tenantSlug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { log.LogError(ex, "Autonomous acquisition queue worker iteration failed"); }
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

    private async Task ProcessDefaultDatabaseAsync(CancellationToken ct) { using var scope = scopes.CreateScope(); await ProcessScopeAsync(scope.ServiceProvider, ct); }
    private async Task ProcessTenantDatabaseAsync(Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));
        await ProcessScopeAsync(scope.ServiceProvider, ct);
    }
    private async Task ProcessScopeAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var ids = await db.AutonomousAcquisitionAgentRuns.Where(x => x.Status == AutonomousAgentRunStatus.Queued).OrderBy(x => x.ScheduledAtUtc).Take(10).Select(x => x.Id).ToListAsync(ct);
        var orchestrator = services.GetRequiredService<IAutonomousAcquisitionRunOrchestrator>();
        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;
            try { await orchestrator.ExecuteAsync(id, ct); }
            catch (Exception ex) { log.LogError(ex, "Autonomous acquisition run {RunId} failed", id); }
        }
    }
    private async Task<Guid?> FindTenantIdAsync(string tenantSlug, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tenants.Where(x => x.Slug == tenantSlug).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    }
}

public sealed class AutonomousAcquisitionSchedulerWorker(IServiceScopeFactory scopes, ILogger<AutonomousAcquisitionSchedulerWorker> log) : BackgroundService
{
    private const string TimeZoneSetting = "acquisition.schedule.timeZoneId";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ScheduleDefaultDatabaseAsync(stoppingToken);
                using var rootScope = scopes.CreateScope();
                var resolver = rootScope.ServiceProvider.GetRequiredService<ITenantDatabaseConnectionResolver>();
                foreach (var tenantSlug in resolver.GetConfiguredTenantDatabases().Keys)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    var tenantId = await FindTenantIdAsync(tenantSlug, stoppingToken);
                    if (tenantId is null) continue;
                    await ScheduleTenantDatabaseAsync(tenantId.Value, tenantSlug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { log.LogError(ex, "Autonomous acquisition scheduler iteration failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ScheduleDefaultDatabaseAsync(CancellationToken ct) { using var scope = scopes.CreateScope(); await ScheduleScopeAsync(scope.ServiceProvider, ct); }
    private async Task ScheduleTenantDatabaseAsync(Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));
        await ScheduleScopeAsync(scope.ServiceProvider, ct);
    }

    private static async Task ScheduleScopeAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var agents = await db.AutonomousAcquisitionAgents.Where(x => x.Status == AutonomousAgentStatus.Active).ToListAsync(ct);
        var tenantIds = agents.Select(x => x.TenantId).Distinct().ToList();
        var timeZones = await db.TenantSettings.Where(x => tenantIds.Contains(x.TenantId) && x.Key == TimeZoneSetting).ToDictionaryAsync(x => x.TenantId, x => x.Value, ct);
        var nowUtc = DateTime.UtcNow;

        foreach (var agent in agents)
        {
            var timeZone = ResolveTimeZone(timeZones.TryGetValue(agent.TenantId, out var configured) ? configured : null);
            var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
            var localToday = DateOnly.FromDateTime(localNow);
            var due = localNow.TimeOfDay >= agent.RunTimeUtc.ToTimeSpan();
            var already = agent.LastRunAtUtc.HasValue && DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agent.LastRunAtUtc.Value, timeZone)) == localToday;
            if (!due || already) continue;

            db.AutonomousAcquisitionAgentRuns.Add(new AutonomousAcquisitionAgentRun
            {
                TenantId = agent.TenantId,
                AgentId = agent.Id,
                IsManual = false,
                Status = AutonomousAgentRunStatus.Queued,
                ScheduledAtUtc = nowUtc
            });
            agent.LastRunAtUtc = nowUtc;
            agent.UpdatedAtUtc = nowUtc;
        }
        await db.SaveChangesAsync(ct);
    }

    private static TimeZoneInfo ResolveTimeZone(string? id)
    {
        if (!string.IsNullOrWhiteSpace(id))
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Utc;
    }

    private async Task<Guid?> FindTenantIdAsync(string tenantSlug, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tenants.Where(x => x.Slug == tenantSlug).Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
    }
}
