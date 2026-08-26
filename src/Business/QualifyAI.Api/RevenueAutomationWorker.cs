using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api;

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
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var automation = scope.ServiceProvider.GetRequiredService<SalesAutomationService>();
                var tenants = await db.Tenants.Where(x => x.IsActive).Select(x => x.Id).ToListAsync(stoppingToken);
                foreach (var tenantId in tenants)
                {
                    var result = await automation.RunAsync(tenantId, stoppingToken);
                    if (result.OpportunitiesCreated > 0 || result.TasksCreated > 0)
                        logger.LogInformation("Revenue automation tenant {TenantId}: {Opportunities} opportunities, {Tasks} tasks, {Pipeline} pipeline", tenantId, result.OpportunitiesCreated, result.TasksCreated, result.PipelineCreated);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Revenue automation cycle failed"); }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
