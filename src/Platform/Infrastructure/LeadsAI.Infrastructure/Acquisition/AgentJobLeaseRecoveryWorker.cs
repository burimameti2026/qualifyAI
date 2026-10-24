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
        // Older runtime records may be Running without a lease due to legacy data or an
        // interrupted transition. Recover those only after the same stale window used
        // for the normal lease, rather than leaving them Running forever.
        var staleLeaseCutoff = now.AddMinutes(-10);

        var expired = await db.AgentJobs
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.Status == AgentJobStatus.Running &&
                        ((x.LeaseUntilUtc.HasValue && x.LeaseUntilUtc.Value <= now) ||
                         (!x.LeaseUntilUtc.HasValue && x.UpdatedAtUtc <= staleLeaseCutoff)))
            .Select(x => new { x.Id, x.AttemptCount, x.MaxAttempts, x.ContainerId, x.ContainerVersion })
            .Take(100)
            .ToListAsync(ct);

        foreach (var item in expired)
        {
            // Keep the job transition, task reconciliation, container state and activity
            // record in one transaction. Otherwise a process crash between writes can
            // leave a requeued job paired with a permanently Running task/container.
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var terminal = item.AttemptCount >= item.MaxAttempts;
            var nextStatus = terminal ? AgentJobStatus.Failed : AgentJobStatus.Queued;
            var error = terminal
                ? "Job lease expired and maximum attempts were exhausted."
                : "Worker lease expired; job returned to the queue for retry.";

            var changed = await db.AgentJobs
                .Where(x => x.TenantId == tenantId &&
                            x.Id == item.Id &&
                            x.Status == AgentJobStatus.Running &&
                            ((x.LeaseUntilUtc.HasValue && x.LeaseUntilUtc.Value <= now) ||
                             (!x.LeaseUntilUtc.HasValue && x.UpdatedAtUtc <= staleLeaseCutoff)))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, nextStatus)
                    .SetProperty(x => x.Error, error)
                    .SetProperty(x => x.LeaseUntilUtc, (DateTime?)null)
                    .SetProperty(x => x.WorkerId, (string?)null)
                    .SetProperty(x => x.CompletedAtUtc, terminal ? now : (DateTime?)null)
                    .SetProperty(x => x.UpdatedAtUtc, now), ct);

            if (changed == 0)
            {
                await tx.RollbackAsync(ct);
                continue;
            }

            // A crashed worker can leave its current task marked Running forever.
            // Reconcile that step with the job recovery decision before another worker
            // starts the job, so history does not show a permanently active task.
            await db.AutonomousAcquisitionTasks
                .Where(x => x.TenantId == tenantId &&
                            x.RunId == item.Id &&
                            x.Status == AutonomousAgentTaskStatus.Running)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, terminal ? AutonomousAgentTaskStatus.Failed : AutonomousAgentTaskStatus.Pending)
                    .SetProperty(x => x.Error, terminal ? error : (string?)null)
                    .SetProperty(x => x.CompletedAtUtc, terminal ? now : (DateTime?)null)
                    .SetProperty(x => x.UpdatedAtUtc, now), ct);

            if (item.ContainerId.HasValue)
            {
                var container = await db.CampaignContainers
                    .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Id == item.ContainerId.Value, ct);

                if (container is not null)
                {
                    if (terminal)
                    {
                        containers.Fail(container, now);
                        db.CampaignContainerActivities.Add(new CampaignContainerActivityRecord
                        {
                            TenantId = tenantId,
                            ContainerId = container.Id,
                            RunId = item.Id,
                            ContainerVersion = item.ContainerVersion ?? container.Version,
                            AtUtc = now,
                            Level = "error",
                            EventType = "container.failed",
                            StepType = string.Empty,
                            StepName = string.Empty,
                            Message = "Container failed because its worker lease expired and retry attempts were exhausted.",
                            DataJson = System.Text.Json.JsonSerializer.Serialize(new { attempt = item.AttemptCount, maxAttempts = item.MaxAttempts })
                        });
                    }
                    else
                    {
                        containers.RequeueAfterLeaseExpiry(container, now);
                        db.CampaignContainerActivities.Add(new CampaignContainerActivityRecord
                        {
                            TenantId = tenantId,
                            ContainerId = container.Id,
                            RunId = item.Id,
                            ContainerVersion = item.ContainerVersion ?? container.Version,
                            AtUtc = now,
                            Level = "warning",
                            EventType = "container.requeued",
                            StepType = string.Empty,
                            StepName = string.Empty,
                            Message = "Container was requeued after its worker lease expired.",
                            DataJson = System.Text.Json.JsonSerializer.Serialize(new { attempt = item.AttemptCount, maxAttempts = item.MaxAttempts })
                        });
                    }
                }
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            log.LogWarning(
                "Recovered expired AgentJob {JobId} for tenant {TenantId}; status {Status}, attempt {Attempt}/{MaxAttempts}",
                item.Id, tenantId, nextStatus, item.AttemptCount, item.MaxAttempts);
        }
    }
}
