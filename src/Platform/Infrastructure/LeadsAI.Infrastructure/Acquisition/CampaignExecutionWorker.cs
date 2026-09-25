using LeadsAI.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeadsAI.Infrastructure.Acquisition;

/// <summary>
/// Advances active campaigns after the initial launch. Due recipients are turned into
/// approval-controlled outreach messages without bypassing the human approval gate.
/// </summary>
public sealed class CampaignExecutionWorker(IServiceScopeFactory scopes, ILogger<CampaignExecutionWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var rootScope = scopes.CreateAsyncScope();
                var services = rootScope.ServiceProvider;
                var runtime = services.GetRequiredService<TenantWorkerRuntime>();
                var tenants = await runtime.EnabledActiveTenantsAsync(TenantWorkerKeys.AcquisitionCampaign, stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    await ProcessTenantAsync(services, tenant.Id, tenant.Slug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                log.LogError(ex, "Campaign execution worker iteration failed");
            }
        }
    }

    private async Task ProcessTenantAsync(IServiceProvider rootServices, Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));

        var executor = scope.ServiceProvider.GetRequiredService<CampaignExecutionService>();
        var queued = await executor.QueueDueMessagesAsync(tenantId, ct);
        if (queued > 0)
            log.LogInformation("Queued {Count} approval-controlled campaign messages for tenant {TenantId}", queued, tenantId);
    }
}
