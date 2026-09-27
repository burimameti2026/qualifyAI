using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure;

public sealed class ModuleProvisioningRetryWorker(IServiceScopeFactory scopeFactory, ILogger<ModuleProvisioningRetryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var orchestrator = scope.ServiceProvider.GetRequiredService<IModuleProvisioningOrchestrator>();
                var alerts = scope.ServiceProvider.GetRequiredService<ITenantAlertService>();
                var now = DateTime.UtcNow;
                var due = await db.TenantModuleProvisionings.Where(x => x.Status == "failed" && x.NextRetryAtUtc != null && x.NextRetryAtUtc <= now).Select(x => new { x.TenantId, x.ModuleCode }).ToListAsync(stoppingToken);
                foreach (var group in due.GroupBy(x => x.TenantId))
                {
                    try { await orchestrator.ProvisionAsync(group.Key, group.Select(x => x.ModuleCode).ToArray(), stoppingToken); }
                    catch (Exception ex) { logger.LogError(ex, "Module provisioning retry failed for tenant {TenantId}", group.Key); alerts.Raise(group.Key, "critical", "provisioning_retry_failed", "Automatic module provisioning retry failed"); }
                }
            }
            catch (Exception ex) { logger.LogError(ex, "Module provisioning retry worker iteration failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
