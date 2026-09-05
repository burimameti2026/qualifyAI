using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using QualifyAI.Contracts.Identity;
using QualifyAI.Identity.Persistence.SqlServer;

namespace QualifyAI.Identity.Infrastructure.Messaging;

public sealed class IdentityOutboxPublisherHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<IdentityOutboxPublisherHostedService> logger) : BackgroundService
{
    private const int BatchSize = 50;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var attempted = await PublishBatchAsync(stoppingToken);
                if (attempted == 0)
                    await Task.Delay(IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Identity outbox publisher failed. Retrying.");
                await Task.Delay(ErrorDelay, stoppingToken);
            }
        }
    }

    private async Task<int> PublishBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
        var utcNow = DateTime.UtcNow;

        var messages = await dbContext.OutboxMessages
            .Where(x =>
                x.ProcessedAtUtc == null &&
                (x.NextAttemptAtUtc == null || x.NextAttemptAtUtc <= utcNow))
            .OrderBy(x => x.OccurredAtUtc)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.Type, throwOnError: false);
                if (eventType is null)
                    throw new InvalidOperationException($"Unable to resolve integration event type '{message.Type}'.");

                var integrationEvent = JsonSerializer.Deserialize(message.Payload, eventType)
                    ?? throw new InvalidOperationException($"Unable to deserialize outbox message '{message.Id}'.");

                integrationEvent = await RepairLegacyLicenseEventAsync(
                    integrationEvent,
                    eventType,
                    dbContext,
                    cancellationToken);

                await publisher.Publish(integrationEvent, eventType, cancellationToken);

                message.ProcessedAtUtc = DateTime.UtcNow;
                message.NextAttemptAtUtc = null;
                message.Error = null;
            }
            catch (Exception exception)
            {
                message.RetryCount++;
                message.NextAttemptAtUtc = DateTime.UtcNow.Add(GetRetryDelay(message.RetryCount));
                message.Error = exception.Message.Length > 4000
                    ? exception.Message[..4000]
                    : exception.Message;

                logger.LogError(
                    exception,
                    "Failed publishing identity outbox message {OutboxMessageId} of type {MessageType}. Retry {RetryCount} at {NextAttemptAtUtc}.",
                    message.Id,
                    message.Type,
                    message.RetryCount,
                    message.NextAttemptAtUtc);
            }
        }

        if (messages.Count > 0)
            await dbContext.SaveChangesAsync(cancellationToken);

        return messages.Count;
    }

    private static async Task<object> RepairLegacyLicenseEventAsync(
        object integrationEvent,
        Type eventType,
        IdentityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (eventType != typeof(TenantLicenseChangedIntegrationEvent)
            || integrationEvent is not TenantLicenseChangedIntegrationEvent message
            || !string.IsNullOrWhiteSpace(message.TenantSlug))
            return integrationEvent;

        var tenantSlug = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == message.TenantId)
            .Select(x => x.Slug)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(tenantSlug))
        {
            throw new InvalidOperationException(
                $"Legacy TenantLicenseChangedIntegrationEvent {message.EventId} for tenant {message.TenantId} has no TenantSlug, and the authoritative Identity tenant record could not supply one.");
        }

        return message with { TenantSlug = tenantSlug.Trim().ToLowerInvariant() };
    }

    private static TimeSpan GetRetryDelay(int retryCount)
    {
        var seconds = Math.Min(300, Math.Pow(2, Math.Min(retryCount, 8)));
        return TimeSpan.FromSeconds(seconds);
    }
}
