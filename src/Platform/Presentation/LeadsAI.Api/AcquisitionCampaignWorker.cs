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
                var resolver = rootScope.ServiceProvider.GetRequiredService<ITenantDatabaseConnectionResolver>();

                foreach (var tenantSlug in resolver.GetConfiguredTenantDatabases().Keys)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    var tenantId = await FindTenantIdAsync(tenantSlug, stoppingToken);
                    if (tenantId is null) continue;

                    if (!await IsTenantActiveAsync(tenantId.Value, stoppingToken))
                    {
                        logger.LogInformation("Skipping campaign queue for inactive tenant {TenantSlug}.", tenantSlug);
                        continue;
                    }

                    await QueueForTenantAsync(tenantId.Value, tenantSlug, stoppingToken);
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

    private async Task<bool> IsTenantActiveAsync(Guid tenantId, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LeadsAI.Persistence.SqlServer.AppDbContext>();
        var now = DateTime.UtcNow;

        return await db.TenantEntitlements.AnyAsync(x =>
            x.TenantId == tenantId &&
            x.TenantStatus == "active" &&
            x.LicenseStatus == "active" &&
            x.StartsAtUtc <= now &&
            (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now), ct);
    }

    private async Task<Guid?> FindTenantIdAsync(string tenantSlug, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<LeadsAI.Persistence.SqlServer.AppDbContext>();

        return await db.Tenants
            .Where(x => x.Slug == tenantSlug)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(ct);
    }
}
