using Microsoft.Extensions.Options;
using LeadsAI.Infrastructure;

namespace LeadsAI.Api;

public sealed class RevenueAutomationOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 300;
}

public sealed class RevenueAutomationWorker(IServiceScopeFactory scopeFactory, IOptions<RevenueAutomationOptions> options, ILogger<RevenueAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;

        var interval = TimeSpan.FromSeconds(Math.Max(60, options.Value.IntervalSeconds));
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var runtime = scope.ServiceProvider.GetRequiredService<TenantWorkerRuntime>();
                var automation = scope.ServiceProvider.GetRequiredService<SalesAutomationService>();
                var tenants = await runtime.EnabledActiveTenantsAsync(TenantWorkerKeys.RevenueAutomation, stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    var result = await automation.RunAsync(tenant.Id, stoppingToken);
                    if (result.OpportunitiesCreated > 0 || result.TasksCreated > 0)
                        logger.LogInformation("Revenue automation tenant {TenantId}: {Opportunities} opportunities, {Tasks} tasks, {Pipeline} pipeline", tenant.Id, result.OpportunitiesCreated, result.TasksCreated, result.PipelineCreated);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception exception) { logger.LogError(exception, "Revenue automation cycle failed."); }
        }
    }
}
