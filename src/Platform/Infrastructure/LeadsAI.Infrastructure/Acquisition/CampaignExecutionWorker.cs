using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LeadsAI.Infrastructure.Acquisition;

public sealed class CampaignExecutionWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<CampaignExecutionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var execution = scope.ServiceProvider.GetRequiredService<CampaignExecutionService>();
                var queued = await execution.QueueDueMessagesAsync(null, stoppingToken);
                if (queued > 0)
                    logger.LogInformation("Campaign execution queued {Count} outreach messages awaiting approval.", queued);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Campaign execution worker iteration failed.");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
