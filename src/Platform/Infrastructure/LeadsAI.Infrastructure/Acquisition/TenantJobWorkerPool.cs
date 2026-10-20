using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using LeadsAI.Application;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

/// <summary>
/// Discovers active tenants continuously and runs isolated worker pools.
/// A tenant pool is long-lived, but its existence never blocks discovery of new tenants.
/// </summary>
public sealed class TenantJobWorkerPool(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<TenantJobWorkerPool> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var concurrency = Math.Max(1,
            configuration.GetValue<int?>("AgentRuntime:DefaultWorkerConcurrency") ?? 2);

        var pools = new Dictionary<Guid, (string Slug, CancellationTokenSource Stop, Task Run)>();

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    List<(Guid Id, string Slug)> tenants;
                    using (var discoveryScope = scopes.CreateScope())
                    {
                        var db = discoveryScope.ServiceProvider.GetRequiredService<AppDbContext>();
                        tenants = await db.Tenants
                            .Where(x => x.IsActive)
                            .Select(x => new ValueTuple<Guid, string>(x.Id, x.Slug))
                            .ToListAsync(stoppingToken);
                    }

                    var activeIds = tenants.Select(x => x.Id).ToHashSet();

                    foreach (var removed in pools.Keys.Where(id => !activeIds.Contains(id)).ToArray())
                    {
                        pools[removed].Stop.Cancel();
                        pools.Remove(removed);
                    }

                    foreach (var tenant in tenants)
                    {
                        if (pools.TryGetValue(tenant.Id, out var existing))
                        {
                            if (existing.Run.IsCompleted)
                            {
                                pools.Remove(tenant.Id);
                                existing.Stop.Dispose();
                            }
                            else
                            {
                                continue;
                            }
                        }

                        var linkedStop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        var run = RunTenantPoolAsync(tenant.Id, tenant.Slug, concurrency, linkedStop.Token);
                        pools[tenant.Id] = (tenant.Slug, linkedStop, run);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Tenant job worker discovery failed");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
        finally
        {
            foreach (var pool in pools.Values)
                pool.Stop.Cancel();

            try { await Task.WhenAll(pools.Values.Select(x => x.Run)); }
            catch (OperationCanceledException) { }

            foreach (var pool in pools.Values)
                pool.Stop.Dispose();
        }
    }

    private async Task RunTenantPoolAsync(
        Guid tenantId,
        string tenantSlug,
        int concurrency,
        CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, concurrency)
            .Select(index => RunTenantWorkerAsync(
                tenantId,
                tenantSlug,
                $"tenant:{tenantId:N}:worker:{index + 1}",
                stoppingToken));

        await Task.WhenAll(workers);
    }

    private async Task RunTenantWorkerAsync(
        Guid tenantId,
        string tenantSlug,
        string workerId,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();

                // Set tenant context before resolving tenant-aware services/DbContext.
                var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
                tenantContext.Set(new CurrentTenant(tenantId, tenantSlug));

                var queue = scope.ServiceProvider.GetRequiredService<IAgentJobQueue>();
                var job = await queue.GetNextJobAndClaimAsync(tenantId, workerId, stoppingToken);

                if (job is null)
                {
                    await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                    continue;
                }

                var executor = scope.ServiceProvider.GetRequiredService<IAgentJobExecutor>();
                await executor.ExecuteAsync(job, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Tenant worker {WorkerId} failed while processing tenant {TenantId}", workerId, tenantId);
                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}

public interface IAgentJobExecutor
{
    Task ExecuteAsync(LeadsAI.Domain.Core.AgentJob job, CancellationToken ct);
}
