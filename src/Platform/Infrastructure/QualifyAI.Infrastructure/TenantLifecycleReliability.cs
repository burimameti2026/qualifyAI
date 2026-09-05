using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure;

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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<ILicenseChangeOrchestrator>();
                var events = scope.ServiceProvider.GetRequiredService<ITenantLifecycleEventStore>();
                var tenantIds = await db.TenantEntitlements.Where(x => x.LicenseStatus == "active" && x.TenantStatus == "active").Select(x => x.TenantId).ToListAsync(stoppingToken);
                foreach (var tenantId in tenantIds)
                {
                    try { await orchestrator.ReconcileAsync(tenantId, stoppingToken); events.Record(new(tenantId, "reconciliation", "completed", "Tenant lifecycle reconciliation completed", DateTime.UtcNow)); }
                    catch (Exception ex) { logger.LogError(ex, "Tenant lifecycle reconciliation failed for {TenantId}", tenantId); events.Record(new(tenantId, "reconciliation", "failed", "Tenant lifecycle reconciliation failed", DateTime.UtcNow)); }
                }
            }
            catch (Exception ex) { logger.LogError(ex, "Tenant lifecycle reconciliation worker iteration failed"); }
            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}
