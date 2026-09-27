using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure;

public sealed class LicenseExpirationWorker(IServiceScopeFactory scopeFactory, ILogger<LicenseExpirationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var events = scope.ServiceProvider.GetRequiredService<ITenantLifecycleEventStore>();
                var alerts = scope.ServiceProvider.GetRequiredService<ITenantAlertService>();
                var now = DateTime.UtcNow;
                var expired = await db.TenantEntitlements.Where(x => x.ExpiresAtUtc != null && x.ExpiresAtUtc <= now && !x.LicenseStatus.Equals("expired")).ToListAsync(stoppingToken);
                foreach (var entitlement in expired)
                {
                    entitlement.LicenseStatus = "expired"; entitlement.TenantStatus = "suspended"; entitlement.UpdatedAtUtc = now;
                    events.Record(new(entitlement.TenantId, "license", "expired", "License expired and tenant suspended", now));
                    alerts.Raise(entitlement.TenantId, "critical", "license_expired", "License expired and the tenant was automatically suspended");
                }
                if (expired.Count > 0) await db.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) { logger.LogError(ex, "License expiration worker iteration failed"); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
