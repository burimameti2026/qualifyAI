using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LeadsAI.Application;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

/// <summary>
/// Recovers jobs whose worker lease expired. Recovery is conditional and atomic so
/// a job that has already renewed its lease cannot be reclaimed by this worker.
/// </summary>
public sealed class AgentJobLeaseRecoveryWorker(
    IServiceScopeFactory scopes,
    ILogger<AgentJobLeaseRecoveryWorker> log) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                List<(Guid Id, string Slug)> tenants;
                using (var discoveryScope = scopes.CreateScope())
                {
                    var db = discoveryScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var activeTenants = await db.Tenants
                        .Where(x => x.IsActive)
                        .Select(x => new { x.Id, x.Slug })
                        .ToListAsync(stoppingToken);
                    tenants = activeTenants.Select(x => (x.Id, x.Slug)).ToList();
                }

                foreach (var tenant in tenants)
                {
                    stoppingToken.ThrowIfCancellationRequested();
                    await RecoverTenantAsync(tenant.Id, tenant.Slug, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "AgentJob lease recovery pass failed");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }

    private async Task RecoverTenantAsync(Guid tenantId, string tenantSlug, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var containers = scope.ServiceProvider.GetRequiredService<ICampaignContainerRuntime>();
        var now = DateTime.UtcNow;

        var expired = await db.AgentJobs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.Status == AgentJobStatus.Running &&
                        x.LeaseUntilUtc.HasValue &&
                        x.LeaseUntilUtc.Value <= now)
            .Select(x => new { x.Id, x.AttemptCount, x.MaxAttempts, x.ContainerId })
            .Take(100)
            .ToListAsync(ct);

        foreach (var item in expired)
        {
            var terminal = item.AttemptCount >= item.MaxAttempts;
            var nextStatus = terminal ? AgentJobStatus.Failed : AgentJobStatus.Queued;
            var error = terminal
                ? "Job lease expired and maximum attempts were exhausted."
                : "Worker lease expired; job returned to the queue for retry.";

            var changed = await db.AgentJobs
                .Where(x => x.TenantId == tenantId &&
                            x.Id == item.Id &&
                            x.Status == AgentJobStatus.Running &&
                            x.LeaseUntilUtc.HasValue &&
                            x.LeaseUntilUtc.Value <= now)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, nextStatus)
                    .SetProperty(x => x.Error, error)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(x => x.WorkerId, (string?)null)
                    .SetProperty(x => x.CompletedAtUtc, terminal ? now : (DateTime?)null)
                    .SetProperty(x => x.UpdatedAtUtc, now), ct);

            if (changed == 0)
                continue;

            if (item.ContainerId.HasValue)
            {
                var container = await db.CampaignContainers
                    .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == item.ContainerId.Value, ct);

                if (container is not null)
                {
                    if (terminal)
                        containers.Fail(container, now);
                    else
                        containers.RequeueAfterLeaseExpiry(container, now);
                }
            }

            log.LogWarning(
                "Recovered expired AgentJob {JobId} for tenant {TenantId}; status {Status}, attempt {Attempt}/{MaxAttempts}",
                item.Id, tenantId, nextStatus, item.AttemptCount, item.MaxAttempts);
        }

        await db.SaveChangesAsync(ct);
    }
}
