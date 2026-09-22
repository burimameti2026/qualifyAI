using MassTransit;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Application.Abstractions.Persistence;
using LeadsAI.BuildingBlocks.Messaging.Inbox;
using LeadsAI.Contracts.Identity;

namespace LeadsAI.Infrastructure.Messaging.Consumers;

public sealed class TenantCreatedConsumer(IdentityEntitlementInboxProcessor processor)
    : IConsumer<TenantCreatedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TenantCreatedIntegrationEvent> context)
        => processor.ProcessTenantCreatedAsync(context.Message, context.CancellationToken);
}

public sealed class TenantStatusChangedConsumer(IdentityEntitlementInboxProcessor processor)
    : IConsumer<TenantStatusChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TenantStatusChangedIntegrationEvent> context)
        => processor.ProcessTenantStatusChangedAsync(context.Message, context.CancellationToken);
}

public sealed class TenantLicenseChangedConsumer(IdentityEntitlementInboxProcessor processor)
    : IConsumer<TenantLicenseChangedIntegrationEvent>
{
    public Task Consume(ConsumeContext<TenantLicenseChangedIntegrationEvent> context)
        => processor.ProcessLicenseChangedAsync(context.Message, context.CancellationToken);
}

public sealed class IdentityEntitlementInboxProcessor(
    AppDbContext dbContext,
    ITenantEntitlementRepository entitlements,
    ILicenseChangeOrchestrator licenseChanges,
    ITenantLifecycleEventStore events)
{
    public Task ProcessTenantCreatedAsync(
        TenantCreatedIntegrationEvent message,
        CancellationToken ct)
        => ProcessOnceAsync(
            message.EventId,
            nameof(TenantCreatedConsumer),
            async () =>
            {
                var tenantSlug = RequireTenantSlug(message.TenantSlug, message.TenantId);
                await entitlements.UpsertTenantAsync(
                    message.TenantId,
                    tenantSlug,
                    "pending",
                    message.OccurredAtUtc,
                    ct);

                events.Record(new(
                    message.TenantId,
                    "tenant",
                    "created",
                    "Tenant created",
                    message.OccurredAtUtc));
            },
            ct);

    public Task ProcessTenantStatusChangedAsync(
        TenantStatusChangedIntegrationEvent message,
        CancellationToken ct)
        => ProcessOnceAsync(
            message.EventId,
            nameof(TenantStatusChangedConsumer),
            async () =>
            {
                var tenantSlug = ResolveLifecycleSlug(message.TenantSlug, message.TenantId);
                await entitlements.UpsertTenantAsync(
                    message.TenantId,
                    tenantSlug,
                    message.Status,
                    message.OccurredAtUtc,
                    ct);

                events.Record(new(
                    message.TenantId,
                    "tenant",
                    message.Status,
                    $"Tenant status changed to {message.Status}",
                    message.OccurredAtUtc));
            },
            ct);

    public Task ProcessLicenseChangedAsync(
        TenantLicenseChangedIntegrationEvent message,
        CancellationToken ct)
        => ProcessOnceAsync(
            message.EventId,
            nameof(TenantLicenseChangedConsumer),
            async () =>
            {
                var tenantSlug = RequireTenantSlug(message.TenantSlug, message.TenantId);

                var tenantStatus =
                    message.Status.Equals("active", StringComparison.OrdinalIgnoreCase)
                        ? "active"
                        : message.Status.Equals("expired", StringComparison.OrdinalIgnoreCase)
                          || message.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase)
                            ? "suspended"
                            : "pending";

                await entitlements.UpsertTenantAsync(
                    message.TenantId,
                    tenantSlug,
                    tenantStatus,
                    message.OccurredAtUtc,
                    ct);

                await entitlements.UpsertLicenseAsync(
                    message.TenantId,
                    message.Plan,
                    message.Status,
                    message.MaxUsers,
                    message.StartsAtUtc,
                    message.ExpiresAtUtc,
                    message.Version,
                    message.Modules,
                    new Dictionary<string, int>
                    {
                        ["users"]=Math.Max(0, message.MaxUsers)
                    },
                    message.OccurredAtUtc,
                    ct);

                if(message.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await licenseChanges.ReconcileAsync(message.TenantId, ct);
                    var status = result.AddedModules.Count>0||result.RemovedModules.Count>0
                        ? "changed"
                        : "renewed";

                    events.Record(new(
                        message.TenantId,
                        "license",
                        status,
                        status=="renewed"
                            ? "License renewed and tenant reactivated"
                            : "License entitlements changed",
                        message.OccurredAtUtc,
                        new Dictionary<string, string>
                        {
                            ["plan"]=message.Plan,
                            ["version"]=message.Version.ToString()
                        }));

                    events.Record(new(
                        message.TenantId,
                        "tenant",
                        "active",
                        "Tenant access active",
                        message.OccurredAtUtc));
                }
                else if(message.Status.Equals("expired", StringComparison.OrdinalIgnoreCase)
                      ||message.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase))
                {
                    events.Record(new(
                        message.TenantId,
                        "license",
                        message.Status,
                        $"License status changed to {message.Status}",
                        message.OccurredAtUtc));

                    events.Record(new(
                        message.TenantId,
                        "tenant",
                        "suspended",
                        "Tenant suspended",
                        message.OccurredAtUtc));
                }
            },
            ct);

    private static string RequireTenantSlug(string? slug, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new InvalidOperationException(
                $"Identity entitlement event for tenant {tenantId} does not contain TenantSlug.");
        }

        return slug.Trim().ToLowerInvariant();
    }

    private async Task ProcessOnceAsync(
        Guid eventId,
        string consumer,
        Func<Task> apply,
        CancellationToken ct)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Every retry must start with a clean tracker. Otherwise an Added tenant
            // from a failed attempt can collide with the same TenantId on the retry.
            dbContext.ChangeTracker.Clear();

            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            var inbox = dbContext.Set<InboxMessage>();
            if (await inbox.AsNoTracking().AnyAsync(
                    x => x.Id == eventId && x.Consumer == consumer,
                    ct))
            {
                await transaction.CommitAsync(ct);
                return;
            }

            await apply();

            inbox.Add(new InboxMessage
            {
                Id = eventId,
                Consumer = consumer,
                ReceivedAtUtc = DateTime.UtcNow,
                ProcessedAtUtc = DateTime.UtcNow
            });

            // Repositories only mutate tracked state. The entitlement projection,
            // lifecycle events and inbox record are persisted atomically here.
            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}