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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var rootScope = scopes.CreateScope();
                var db = rootScope.ServiceProvider.GetRequiredService<AppDbContext>();

                // TenantEntitlements is the authoritative business tenant registry.
                // TenantDatabases is only connection-routing configuration and may contain
                // the master FindLeadsAI database rather than customer tenants.
                var tenants = await db.TenantEntitlements
                    .AsNoTracking()
                    .Where(x =>
                        x.TenantId != Guid.Empty &&
                        !string.IsNullOrWhiteSpace(x.TenantSlug) &&
                        x.TenantStatus == "active" &&
                        x.LicenseStatus == "active" &&
                        x.StartsAtUtc <= DateTime.UtcNow &&
                        (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > DateTime.UtcNow))
                    .Select(x => new { Id = x.TenantId, Slug = x.TenantSlug })
                    .ToListAsync(stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ProcessTenantDatabaseAsync(tenant.Id, tenant.Slug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { log.LogError(ex, "Autonomous acquisition queue worker iteration failed"); }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }

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
        var tenantId = services.GetRequiredService<ITenantContext>().Current?.Id ?? throw new InvalidOperationException("Tenant context is not set.");

        var ids = await db.AutonomousAcquisitionAgentRuns
            .Where(x => x.TenantId == tenantId && x.Status == AutonomousAgentRunStatus.Queued)
            .OrderBy(x => x.ScheduledAtUtc)
            .Take(10)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var orchestrator = services.GetRequiredService<IAutonomousAcquisitionRunOrchestrator>();

        foreach (var id in ids)
        {
            if (ct.IsCancellationRequested) break;

            try { await orchestrator.ExecuteAsync(id, ct); }
            catch (Exception ex) { log.LogError(ex, "Autonomous acquisition run {RunId} failed", id); }
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
                using var rootScope = scopes.CreateScope();
                var db = rootScope.ServiceProvider.GetRequiredService<AppDbContext>();

                // TenantEntitlements is the authoritative business tenant registry.
                // TenantDatabases is only a connection-routing map and must not be mistaken
                // for the tenant list.
                var tenants = await db.TenantEntitlements
                    .AsNoTracking()
                    .Where(x =>
                        x.TenantId != Guid.Empty &&
                        !string.IsNullOrWhiteSpace(x.TenantSlug) &&
                        x.TenantStatus == "active" &&
                        x.LicenseStatus == "active" &&
                        x.StartsAtUtc <= DateTime.UtcNow &&
                        (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > DateTime.UtcNow))
                    .Select(x => new { Id = x.TenantId, Slug = x.TenantSlug })
                    .ToListAsync(stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ScheduleTenantDatabaseAsync(tenant.Id, tenant.Slug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { log.LogError(ex, "Autonomous acquisition scheduler iteration failed"); }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

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
        var tenantId = services.GetRequiredService<ITenantContext>().Current?.Id ?? throw new InvalidOperationException("Tenant context is not set.");

        var agents = await db.AutonomousAcquisitionAgents
            .Where(x => x.TenantId == tenantId && x.Status == AutonomousAgentStatus.Active)
            .ToListAsync(ct);

        var timeZoneId = await db.TenantSettings
            .Where(x => x.TenantId == tenantId && x.Key == TimeZoneSetting)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);

        var timeZone = ResolveTimeZone(timeZoneId);
        var pendingAgentIds = await db.AutonomousAcquisitionAgentRuns
            .Where(x => x.TenantId == tenantId &&
                        (x.Status == AutonomousAgentRunStatus.Queued || x.Status == AutonomousAgentRunStatus.Running))
            .Select(x => x.AgentId)
            .Distinct()
            .ToListAsync(ct);

        var pending = pendingAgentIds.ToHashSet();
        var nowUtc = DateTime.UtcNow;
        var localNow = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
        var localToday = DateOnly.FromDateTime(localNow);

        foreach (var agent in agents)
        {
            if (pending.Contains(agent.Id)) continue;

            var due = localNow.TimeOfDay >= agent.RunTimeUtc.ToTimeSpan();
            var already = agent.LastRunAtUtc.HasValue &&
                          DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(agent.LastRunAtUtc.Value, timeZone)) == localToday;

            if (!due || already) continue;

            db.AutonomousAcquisitionAgentRuns.Add(new AutonomousAcquisitionAgentRun
            {
                TenantId = tenantId,
                AgentId = agent.Id,
                IsManual = false,
                Status = AutonomousAgentRunStatus.Queued,
                ScheduledAtUtc = nowUtc
            });

            pending.Add(agent.Id);
            agent.UpdatedAtUtc = nowUtc;
        }

        await db.SaveChangesAsync(ct);
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



public sealed class AutonomousAcquisitionEnrichmentWorker(IServiceScopeFactory scopes, ILogger<AutonomousAcquisitionEnrichmentWorker> log) : BackgroundService
{
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var rootScope = scopes.CreateScope();
                var db = rootScope.ServiceProvider.GetRequiredService<AppDbContext>();

                var tenants = await db.TenantEntitlements
                    .AsNoTracking()
                    .Where(x =>
                        x.TenantId != Guid.Empty &&
                        !string.IsNullOrWhiteSpace(x.TenantSlug) &&
                        x.TenantStatus == "active" &&
                        x.LicenseStatus == "active" &&
                        x.StartsAtUtc <= DateTime.UtcNow &&
                        (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > DateTime.UtcNow))
                    .Select(x => new { x.TenantId, x.TenantSlug })
                    .ToListAsync(stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await EnrichTenantAsync(tenant.TenantId, tenant.TenantSlug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                log.LogError(ex, "Autonomous acquisition enrichment worker iteration failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task EnrichTenantAsync(Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var backend = scope.ServiceProvider.GetRequiredService<IAutonomousAcquisitionBackendService>();

        var prospects = await db.Prospects
            .Where(x => x.TenantId == tenantId && x.Status == ProspectStatus.Discovered)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var prospect in prospects)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var agent = await db.AutonomousAcquisitionAgents
                    .Where(x => x.TenantId == tenantId && x.Status == AutonomousAgentStatus.Active)
                    .OrderBy(x => x.UpdatedAtUtc)
                    .Select(x => new { x.Id, x.MinimumScore })
                    .FirstOrDefaultAsync(ct);

                if (agent is null)
                    continue;

                await backend.ResearchAsync(tenantId, agent.Id, prospect, agent.MinimumScore, ct);
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Enrichment failed for prospect {ProspectId} in tenant {TenantId}", prospect.Id, tenantId);
            }
        }
    }
}
