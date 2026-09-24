using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LeadsAI.Application;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

/// <summary>
/// Advances active campaigns after the initial launch. Due recipients are turned into
/// approval-controlled outreach messages without bypassing the human approval gate.
/// </summary>
public sealed class CampaignExecutionWorker(IServiceScopeFactory scopes, ILogger<CampaignExecutionWorker> log) : BackgroundService
{
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
                    await ProcessTenantAsync(tenant.TenantId, tenant.TenantSlug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                log.LogError(ex, "Campaign execution worker iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessTenantAsync(Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));

        var executor = scope.ServiceProvider.GetRequiredService<CampaignExecutionService>();
        var queued = await executor.QueueDueMessagesAsync(tenantId, ct);
        if (queued > 0)
            log.LogInformation("Queued {Count} approval-controlled campaign messages for tenant {TenantId}", queued, tenantId);
    }
}
