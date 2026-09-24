using Microsoft.EntityFrameworkCore;
using LeadsAI.Application;
using LeadsAI.Infrastructure;
using LeadsAI.Infrastructure.Acquisition;

namespace LeadsAI.Api;

public sealed class AcquisitionCampaignWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<AcquisitionCampaignWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var rootScope = scopeFactory.CreateAsyncScope();
                var db = rootScope.ServiceProvider.GetRequiredService<LeadsAI.Persistence.SqlServer.AppDbContext>();
                var now = DateTime.UtcNow;

                var tenants = await db.TenantEntitlements
                    .AsNoTracking()
                    .Where(x =>
                        x.TenantId != Guid.Empty &&
                        !string.IsNullOrWhiteSpace(x.TenantSlug) &&
                        x.TenantStatus == "active" &&
                        x.LicenseStatus == "active" &&
                        x.StartsAtUtc <= now &&
                        (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now))
                    .Select(x => new { x.TenantId, x.TenantSlug })
                    .ToListAsync(stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await QueueForTenantAsync(tenant.TenantId, tenant.TenantSlug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Campaign scheduler iteration failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task QueueForTenantAsync(Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));

        var queued = await scope.ServiceProvider.GetRequiredService<CampaignExecutionService>()
            .QueueDueMessagesAsync(tenantId, ct);

        if (queued > 0)
            logger.LogInformation("Tenant {TenantSlug}: queued {Count} due campaign messages.", tenantSlug, queued);
    }

}
