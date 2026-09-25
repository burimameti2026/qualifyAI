using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Email;

/// <summary>
/// Sends only outreach messages that have an explicit completed approval task.
/// Tenant selection is controlled by TenantWorkerRuntime; no tenant is processed
/// unless that tenant enabled the outreach-delivery worker.
/// </summary>
public sealed class OutreachDeliveryWorker(
    IServiceScopeFactory scopes,
    ILogger<OutreachDeliveryWorker> log) : BackgroundService
{
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(20));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var rootScope = scopes.CreateAsyncScope();
                var services = rootScope.ServiceProvider;
                var runtime = services.GetRequiredService<TenantWorkerRuntime>();
                var delivery = services.GetRequiredService<EmailDeliveryService>();
                var tenants = await runtime.EnabledActiveTenantsAsync(
                    TenantWorkerKeys.OutreachDelivery,
                    stoppingToken);

                foreach (var tenant in tenants)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    await using var tenantScope = services.CreateAsyncScope();
                    var tenantContext = tenantScope.ServiceProvider.GetRequiredService<ITenantContext>();
                    tenantContext.Set(new CurrentTenant(tenant.Id, tenant.Slug));

                    var db = tenantScope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var approvedIds = await (
                        from message in db.OutreachMessages
                        join campaign in db.Campaigns
                            on new { message.TenantId, message.CampaignId }
                            equals new { campaign.TenantId, campaign.Id }
                        where message.TenantId == tenant.Id
                              && message.Status == OutreachStatus.Queued
                              && campaign.Status == CampaignStatus.Running
                              && db.CrmTasks.Any(task =>
                                  task.TenantId == tenant.Id &&
                                  task.Title == "APPROVAL: Send outreach " + message.Id &&
                                  task.Completed)
                        orderby message.CreatedAtUtc
                        select message.Id)
                        .Take(BatchSize)
                        .ToListAsync(stoppingToken);

                    foreach (var messageId in approvedIds)
                    {
                        if (stoppingToken.IsCancellationRequested) break;

                        var result = await delivery.SendApprovedAsync(
                            tenant.Id,
                            messageId,
                            stoppingToken);

                        if (!result.Success)
                            log.LogWarning(
                                "Approved outreach {MessageId} was not sent for tenant {TenantId}: {Error}",
                                messageId,
                                tenant.Id,
                                result.Error);
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                log.LogError(ex, "Outreach delivery worker iteration failed");
            }
        }
    }
}
