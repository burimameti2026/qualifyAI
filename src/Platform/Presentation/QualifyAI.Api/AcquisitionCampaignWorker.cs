using QualifyAI.Application;
using QualifyAI.Infrastructure.Acquisition;

namespace QualifyAI.Api;

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
                await QueueForDefaultDatabaseAsync(stoppingToken);

                await using var rootScope = scopeFactory.CreateAsyncScope();
                var resolver = rootScope.ServiceProvider.GetRequiredService<ITenantDatabaseConnectionResolver>();
                foreach (var tenantSlug in resolver.GetConfiguredTenantDatabases().Keys)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    var tenantId = await FindTenantIdAsync(tenantSlug, stoppingToken);
                    if (tenantId is null) continue;
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

    private async Task QueueForDefaultDatabaseAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await QueueForScopeAsync(scope.ServiceProvider, ct);
    }

    private async Task QueueForTenantAsync(Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));
        await QueueForScopeAsync(scope.ServiceProvider, ct);
    }

    private async Task QueueForScopeAsync(IServiceProvider services, CancellationToken ct)
    {
        var queued = await services.GetRequiredService<CampaignExecutionService>()
            .QueueDueMessagesAsync(null, ct);
        if (queued > 0) logger.LogInformation("Queued {Count} due campaign messages.", queued);
    }

    private async Task<Guid?> FindTenantIdAsync(string tenantSlug, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<QualifyAI.Persistence.SqlServer.AppDbContext>();
        return await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleOrDefaultAsync(
            db.Tenants.Where(x => x.Slug == tenantSlug).Select(x => (Guid?)x.Id), ct);
    }
}
