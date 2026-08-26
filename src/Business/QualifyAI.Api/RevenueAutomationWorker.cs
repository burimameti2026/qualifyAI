using Microsoft.Extensions.Options;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api;

public sealed class RevenueAutomationOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 300;
}

public sealed class RevenueAutomationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RevenueAutomationOptions> options,
    ILogger<RevenueAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
            return;

        var interval = TimeSpan.FromSeconds(Math.Max(60, options.Value.IntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var tenants = scope.ServiceProvider.GetRequiredService<ITenantProjectionRepository>();
                var automation = scope.ServiceProvider.GetRequiredService<SalesAutomationService>();

                var tenantIds = await tenants.ListActiveTenantIdsAsync(stoppingToken);
                foreach (var tenantId in tenantIds)
                {
                    var result = await automation.RunAsync(tenantId, stoppingToken);
                    if (result.OpportunitiesCreated > 0 || result.TasksCreated > 0)
                    {
                        logger.LogInformation(
                            "Revenue automation tenant {TenantId}: {Opportunities} opportunities, {Tasks} tasks, {Pipeline} pipeline",
                            tenantId,
                            result.OpportunitiesCreated,
                            result.TasksCreated,
                            result.PipelineCreated);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Revenue automation cycle failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
