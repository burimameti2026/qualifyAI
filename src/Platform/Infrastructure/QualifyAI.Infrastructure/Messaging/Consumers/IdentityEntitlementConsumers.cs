using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.BuildingBlocks.Messaging.Inbox;
using QualifyAI.Contracts.Identity;

namespace QualifyAI.Infrastructure.Messaging.Consumers;

public sealed class TenantCreatedConsumer(IdentityEntitlementInboxProcessor processor) : IConsumer<TenantCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TenantCreatedIntegrationEvent> context) => processor.ProcessTenantCreatedAsync(context.Message, context.CancellationToken);
}
public sealed class TenantStatusChangedConsumer(IdentityEntitlementInboxProcessor processor) : IConsumer<TenantStatusChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TenantStatusChangedIntegrationEvent> context) => processor.ProcessTenantStatusChangedAsync(context.Message, context.CancellationToken);
}
public sealed class TenantLicenseChangedConsumer(IdentityEntitlementInboxProcessor processor) : IConsumer<TenantLicenseChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TenantLicenseChangedIntegrationEvent> context) => processor.ProcessLicenseChangedAsync(context.Message, context.CancellationToken);
}

public sealed class IdentityEntitlementInboxProcessor(AppDbContext dbContext, ITenantEntitlementRepository entitlements, ILicenseChangeOrchestrator licenseChanges)
{
    public Task ProcessTenantCreatedAsync(TenantCreatedIntegrationEvent message, CancellationToken ct) => ProcessOnceAsync(message.EventId, nameof(TenantCreatedConsumer), () => entitlements.UpsertTenantAsync(message.TenantId, message.TenantSlug, "active", message.OccurredAtUtc, ct), ct);
    public Task ProcessTenantStatusChangedAsync(TenantStatusChangedIntegrationEvent message, CancellationToken ct) => ProcessOnceAsync(message.EventId, nameof(TenantStatusChangedConsumer), () => entitlements.UpsertTenantAsync(message.TenantId, message.TenantSlug, message.Status, message.OccurredAtUtc, ct), ct);

    public Task ProcessLicenseChangedAsync(TenantLicenseChangedIntegrationEvent message, CancellationToken ct) => ProcessOnceAsync(message.EventId, nameof(TenantLicenseChangedConsumer), async () =>
    {
        await entitlements.UpsertLicenseAsync(message.TenantId, message.Plan, message.Status, message.MaxUsers, message.StartsAtUtc, message.ExpiresAtUtc, message.Version, message.Modules, new Dictionary<string, int> { ["users"] = Math.Max(0, message.MaxUsers) }, message.OccurredAtUtc, ct);
        if (message.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
        {
            await entitlements.UpsertTenantAsync(message.TenantId, message.TenantSlug, "active", message.OccurredAtUtc, ct);
            await licenseChanges.ReconcileAsync(message.TenantId, ct);
        }
        else if (message.Status.Equals("expired", StringComparison.OrdinalIgnoreCase) || message.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase))
        {
            await entitlements.UpsertTenantAsync(message.TenantId, message.TenantSlug, "suspended", message.OccurredAtUtc, ct);
        }
    }, ct);

    private async Task ProcessOnceAsync(Guid eventId, string consumer, Func<Task> apply, CancellationToken ct)
    {
        var inbox = dbContext.Set<InboxMessage>();
        if (await inbox.AsNoTracking().AnyAsync(x => x.Id == eventId && x.Consumer == consumer, ct)) return;
        await apply();
        inbox.Add(new InboxMessage { Id = eventId, Consumer = consumer, ReceivedAtUtc = DateTime.UtcNow, ProcessedAtUtc = DateTime.UtcNow });
        await dbContext.SaveChangesAsync(ct);
    }
}
