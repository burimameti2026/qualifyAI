using QualifyAI.Infrastructure.Acquisition;

namespace QualifyAI.Api;

public sealed class AcquisitionCampaignWorker(IServiceScopeFactory scopeFactory, ILogger<AcquisitionCampaignWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var queued = await scope.ServiceProvider.GetRequiredService<CampaignExecutionService>()
                    .QueueDueMessagesAsync(null, stoppingToken);
                if (queued > 0) logger.LogInformation("Queued {Count} due campaign messages.", queued);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Campaign scheduler iteration failed.");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }
}
