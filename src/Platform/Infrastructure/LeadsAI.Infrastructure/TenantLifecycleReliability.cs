using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using LeadsAI.Persistence.SqlServer.Projections;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure;

public sealed record TenantLifecycleHealth(Guid TenantId, bool Healthy, int FailedModules, int PendingRetries, int ActiveModules, DateTime CheckedAtUtc);
public interface ITenantLifecycleHealthService { Task<TenantLifecycleHealth> CheckAsync(Guid tenantId, CancellationToken ct = default); }
public sealed class TenantLifecycleHealthService(AppDbContext db) : ITenantLifecycleHealthService
{
    public async Task<TenantLifecycleHealth> CheckAsync(Guid tenantId, CancellationToken ct = default)
    {
        var rows = await db.TenantModuleProvisionings.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var failed = rows.Count(x => x.Status is "failed" or "deactivation_failed");
        var pending = rows.Count(x => x.NextRetryAtUtc != null || x.Status == "provisioning");
        return new(tenantId, failed == 0 && pending == 0, failed, pending, rows.Count(x => x.Status == "completed"), DateTime.UtcNow);
    }
}

public sealed class TenantLifecycleReconciliationWorker(IServiceScopeFactory scopeFactory, ILogger<TenantLifecycleReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ILicenseChangeOrchestrator>();
                var lifecycle = scope.ServiceProvider.GetRequiredService<ITenantLifecycleOrchestrator>();
                var events = scope.ServiceProvider.GetRequiredService<ITenantLifecycleEventStore>();
                var alerts = scope.ServiceProvider.GetRequiredService<ITenantAlertService>();
                var now = DateTime.UtcNow;

                // Lifecycle changes are already handled by event-driven commands. The
                // reconciliation worker only needs to revisit tenants with unfinished or
                // failed module provisioning/deactivation work.
                var tenantIds = await db.TenantModuleProvisionings
                    .AsNoTracking()
                    .Where(x =>
                        x.TenantId != Guid.Empty &&
                        (x.Status == "provisioning" ||
                         x.Status == "failed" ||
                         x.Status == "deactivation_failed" ||
                         (x.NextRetryAtUtc.HasValue && x.NextRetryAtUtc <= now)))
                    .Select(x => x.TenantId)
                    .Distinct()
                    .ToListAsync(stoppingToken);

                // Also reconcile tenants whose entitlement projection is still
                // provisioning/failed even when no module row currently needs retry.
                var entitlementTenants = await db.TenantEntitlements
                    .AsNoTracking()
                    .Where(x =>
                        x.TenantId != Guid.Empty &&
                        x.LicenseStatus == "active" &&
                        x.TenantStatus != "active")
                    .Select(x => new { x.TenantId, x.ModulesJson })
                    .ToListAsync(stoppingToken);

                foreach (var entitlement in entitlementTenants)
                {
                    if (tenantIds.Contains(entitlement.TenantId))
                        continue;

                    try
                    {
                        var modules = JsonSerializer.Deserialize<string[]>(entitlement.ModulesJson) ?? Array.Empty<string>();
                        if (modules.Length > 0)
                            tenantIds.Add(entitlement.TenantId);
                    }
                    catch (JsonException)
                    {
                        logger.LogWarning("Invalid ModulesJson for tenant {TenantId}", entitlement.TenantId);
                    }
                }

                foreach (var tenantId in tenantIds)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        await orchestrator.ReconcileAsync(tenantId, stoppingToken);
                        var entitlement = await db.TenantEntitlements.AsNoTracking()
                            .SingleOrDefaultAsync(x => x.TenantId == tenantId, stoppingToken);
                        if (entitlement is not null && entitlement.LicenseStatus == "active")
                        {
                            var modules = JsonSerializer.Deserialize<string[]>(entitlement.ModulesJson) ?? Array.Empty<string>();
                            if (modules.Length > 0)
                                await lifecycle.ActivateAsync(new TenantLifecycleRequest(tenantId, modules), stoppingToken);
                        }
                        events.Record(new(tenantId, "reconciliation", "completed", "Tenant lifecycle reconciliation completed", DateTime.UtcNow));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "Tenant lifecycle reconciliation failed for {TenantId}", tenantId);
                        events.Record(new(tenantId, "reconciliation", "failed", "Tenant lifecycle reconciliation failed", DateTime.UtcNow));
                        alerts.Raise(tenantId, "critical", "reconciliation_failed", "Tenant lifecycle reconciliation failed");
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Tenant lifecycle reconciliation worker iteration failed"); }
        }
    }
}
