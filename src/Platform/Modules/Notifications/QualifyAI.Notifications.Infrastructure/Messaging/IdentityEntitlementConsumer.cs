using System.Data;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.Entitlements;
using QualifyAI.BuildingBlocks.Messaging.Inbox;
using QualifyAI.Contracts.Identity;
using QualifyAI.Notifications.Persistence.SqlServer;

namespace QualifyAI.Notifications.Infrastructure.Messaging;

public sealed class IdentityEntitlementConsumer(NotificationsDbContext db) :
    IConsumer<TenantCreatedIntegrationEvent>,
    IConsumer<TenantStatusChangedIntegrationEvent>,
    IConsumer<TenantLicenseChangedIntegrationEvent>
{
    private const string ConsumerName = "notifications.identity-entitlements";

    public Task Consume(ConsumeContext<TenantCreatedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.TenantId, context.Message.OccurredAtUtc, async () =>
        {
            var state = await GetOrCreateAsync(context.Message.TenantId, context.CancellationToken);
            state.TenantSlug = RequireTenantSlug(context.Message.TenantSlug, context.Message.TenantId);
            // TenantCreated means the tenant exists; license activation is a separate event.
            state.TenantStatus = "pending";
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<TenantStatusChangedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.OccurredAtUtc, async () =>
        {
            var state = await GetOrCreateAsync(context.Message.TenantId, context.CancellationToken);
            state.TenantSlug = RequireTenantSlug(context.Message.TenantSlug, context.Message.TenantId);
            state.TenantStatus = context.Message.Status.Trim().ToLowerInvariant();
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<TenantLicenseChangedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.OccurredAtUtc, async () =>
        {
            var state = await GetOrCreateAsync(context.Message.TenantId, context.CancellationToken);
            state.TenantSlug = RequireTenantSlug(context.Message.TenantSlug, context.Message.TenantId);
            if (state.Version > context.Message.Version) return;
            state.LicensePlan = context.Message.Plan.Trim().ToLowerInvariant();
            state.LicenseStatus = context.Message.Status.Trim().ToLowerInvariant();
            state.MaxUsers = context.Message.MaxUsers;
            state.StartsAtUtc = context.Message.StartsAtUtc;
            state.ExpiresAtUtc = context.Message.ExpiresAtUtc;
            state.Version = context.Message.Version;
            state.ModulesJson = JsonSerializer.Serialize(context.Message.Modules);
        }, context.CancellationToken);

    private async Task<TenantEntitlementState> GetOrCreateAsync(Guid tenantId, CancellationToken ct)
    {
        var state = await db.TenantEntitlements
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);

        if (state is not null)
            return state;

        state = new TenantEntitlementState
        {
            TenantId = tenantId
        };

        db.TenantEntitlements.Add(state);
        return state;
    }

    private static string RequireTenantSlug(string? slug, Guid tenantId)
    {
        if (string.IsNullOrWhiteSpace(slug))
        {
            throw new InvalidOperationException(
                $"Identity entitlement event for tenant {tenantId} does not contain TenantSlug.");
        }

        return slug.Trim().ToLowerInvariant();
    }

    private async Task ProcessAsync(
        Guid eventId,
        Guid tenantId,
        DateTime occurredAtUtc,
        Func<Task> mutate,
        CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            // Retries reuse the DbContext. Always start from a clean tracker so an
            // Added tenant from a failed attempt cannot collide on the retry.
            db.ChangeTracker.Clear();

            // Idempotency is checked before opening the user transaction.
            // The InboxMessages primary key is the final concurrency guard.
            if (await db.InboxMessages
                    .AsNoTracking()
                    .AnyAsync(x => x.Id == eventId && x.Consumer == ConsumerName, ct))
            {
                return;
            }

            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.ReadCommitted,
                    ct);

            var lockResource =
                $"qualifyai:tenant-entitlement:{ConsumerName}:{tenantId:D}";

            await db.Database.ExecuteSqlRawAsync(
                "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                new object[] { lockResource },
                ct);

            await mutate();

            db.InboxMessages.Add(new InboxMessage
            {
                Id = eventId,
                Consumer = ConsumerName,
                ReceivedAtUtc = DateTime.UtcNow,
                ProcessedAtUtc = DateTime.UtcNow
            });

            var tracked = db.ChangeTracker
                .Entries<TenantEntitlementState>()
                .FirstOrDefault(x => x.State != EntityState.Unchanged)
                ?.Entity;

            if (tracked is not null)
                tracked.UpdatedAtUtc = occurredAtUtc;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
