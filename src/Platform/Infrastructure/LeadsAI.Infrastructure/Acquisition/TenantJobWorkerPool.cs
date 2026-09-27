using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LeadsAI.Persistence.SqlServer;
using Microsoft.Extensions.Configuration;

namespace LeadsAI.Infrastructure.Acquisition;

/// <summary>
/// Runs an isolated worker pool per active tenant. Each tenant gets its own
/// concurrency budget and workers only claim jobs belonging to that tenant.
/// Execution is delegated to IAgentJobExecutor.
/// </summary>
public sealed class TenantJobWorkerPool(
    IServiceScopeFactory scopes,
    IConfiguration configuration,
    ILogger<TenantJobWorkerPool> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var defaultConcurrency = Math.Max(
            1,
            configuration.GetValue<int?>("AgentRuntime:DefaultWorkerConcurrency") ?? 2);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var discoveryScope = scopes.CreateScope();
                var db = discoveryScope.ServiceProvider.GetRequiredService<AppDbContext>();

                var tenantIds = await db.Tenants
                    .Where(x => x.IsActive)
                    .Select(x => x.Id)
                    .ToListAsync(stoppingToken);

                var tasks = tenantIds.Select(tenantId =>
                    RunTenantPoolAsync(tenantId, defaultConcurrency, stoppingToken));

                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Tenant job worker pool iteration failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task RunTenantPoolAsync(
        Guid tenantId,
        int concurrency,
        CancellationToken stoppingToken)
    {
        var workers = Enumerable.Range(0, concurrency)
            .Select(index => RunTenantWorkerAsync(
                tenantId,
                $"tenant:{tenantId:N}:worker:{index + 1}",
                stoppingToken));

        await Task.WhenAll(workers);
    }

    private async Task RunTenantWorkerAsync(
        Guid tenantId,
        string workerId,
        CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var queue = scope.ServiceProvider.GetRequiredService<IAgentJobQueue>();

                var job = await queue.GetNextJobAndClaimAsync(
                    tenantId,
                    workerId,
                    stoppingToken);

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
                log.LogError(
                    ex,
                    "Tenant worker {WorkerId} failed while processing tenant {TenantId}",
                    workerId,
                    tenantId);

                await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
            }
        }
    }
}

public interface IAgentJobExecutor
{
    Task ExecuteAsync(LeadsAI.Domain.Core.AgentJob job, CancellationToken ct);
}
