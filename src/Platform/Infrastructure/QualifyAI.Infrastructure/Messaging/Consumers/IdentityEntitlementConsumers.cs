using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.BuildingBlocks.Messaging.Inbox;
using QualifyAI.Contracts.Identity;

namespace QualifyAI.Infrastructure.Messaging.Consumers;

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
                var tenantSlug = ResolveLifecycleSlug(message.TenantSlug, message.TenantId);
                await entitlements.UpsertTenantAsync(
                    message.TenantId,
                    tenantSlug,
                    "active",
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
                var existing = await entitlements.GetAsync(message.TenantId, ct);
                var tenantSlug = await ResolveSlugAsync(message.TenantId, message.TenantSlug, ct);

                var tenantStatus =
                    message.Status.Equals("active", StringComparison.OrdinalIgnoreCase)
                        ? "active"
                        : message.Status.Equals("expired", StringComparison.OrdinalIgnoreCase)
                          || message.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase)
                            ? "suspended"
                            : existing?.TenantStatus ?? "pending";

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
                        ["users"] = Math.Max(0, message.MaxUsers)
                    },
                    message.OccurredAtUtc,
                    ct);

                if (message.Status.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await licenseChanges.ReconcileAsync(message.TenantId, ct);
                    var status = result.AddedModules.Count > 0 || result.RemovedModules.Count > 0
                        ? "changed"
                        : "renewed";

                    events.Record(new(
                        message.TenantId,
                        "license",
                        status,
                        status == "renewed"
                            ? "License renewed and tenant reactivated"
                            : "License entitlements changed",
                        message.OccurredAtUtc,
                        new Dictionary<string, string>
                        {
                            ["plan"] = message.Plan,
                            ["version"] = message.Version.ToString()
                        }));

                    events.Record(new(
                        message.TenantId,
                        "tenant",
                        "active",
                        "Tenant access active",
                        message.OccurredAtUtc));
                }
                else if (message.Status.Equals("expired", StringComparison.OrdinalIgnoreCase)
                      || message.Status.Equals("suspended", StringComparison.OrdinalIgnoreCase))
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

    private async Task<string> ResolveSlugAsync(
        Guid tenantId,
        string? messageSlug,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(messageSlug))
            return messageSlug.Trim().ToLowerInvariant();

        // Legacy/in-flight license events may predate TenantSlug and the Platform tenant projection.
        // Prefer an existing entitlement projection, then the platform tenant record, and finally
        // use a deterministic internal slug until a TenantCreated event supplies the real slug.
        var projectionSlug = await dbContext.TenantEntitlements
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.TenantSlug != "")
            .Select(x => x.TenantSlug)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(projectionSlug))
            return projectionSlug.Trim().ToLowerInvariant();

        var persistedSlug = await dbContext.Tenants
            .AsNoTracking()
            .Where(x => x.Id == tenantId)
            .Select(x => x.Slug)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(persistedSlug))
            return persistedSlug.Trim().ToLowerInvariant();

        return CreateFallbackSlug(tenantId);
    }

    private static string ResolveLifecycleSlug(string? slug, Guid tenantId)
        => !string.IsNullOrWhiteSpace(slug)
            ? slug.Trim().ToLowerInvariant()
            : CreateFallbackSlug(tenantId);

    private static string CreateFallbackSlug(Guid tenantId)
        => $"tenant-{tenantId:N}";

    private async Task ProcessOnceAsync(
        Guid eventId,
        string consumer,
        Func<Task> apply,
        CancellationToken ct)
    {
        var inbox = dbContext.Set<InboxMessage>();
        if (await inbox.AsNoTracking().AnyAsync(
                x => x.Id == eventId && x.Consumer == consumer,
                ct))
            return;

        await apply();
        inbox.Add(new InboxMessage
        {
            Id = eventId,
            Consumer = consumer,
            ReceivedAtUtc = DateTime.UtcNow,
            ProcessedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync(ct);
    }
}
