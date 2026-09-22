using System.Text.Json;
using Microsoft.Data.SqlClient;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Automation.Persistence.SqlServer;
using LeadsAI.BuildingBlocks.Messaging.Entitlements;
using LeadsAI.BuildingBlocks.Messaging.Inbox;
using LeadsAI.Contracts.Identity;

namespace LeadsAI.Automation.Infrastructure.Messaging;

public sealed class IdentityEntitlementConsumer(AutomationDbContext db) :
    IConsumer<TenantCreatedIntegrationEvent>,
    IConsumer<TenantStatusChangedIntegrationEvent>,
    IConsumer<TenantLicenseChangedIntegrationEvent>
{
    private const string ConsumerName = "automation.identity-entitlements";

    public Task Consume(ConsumeContext<TenantCreatedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.TenantId, context.Message.OccurredAtUtc, async () =>
        {
            var state = await GetOrCreateAsync(context.Message.TenantId, context.CancellationToken);
            state.TenantSlug = RequireTenantSlug(context.Message.TenantSlug, context.Message.TenantId);
            // TenantCreated means the tenant exists; license activation is a separate event.
            state.TenantStatus = "pending";
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<TenantStatusChangedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.TenantId, context.Message.OccurredAtUtc, async () =>
        {
            var state = await GetOrCreateAsync(context.Message.TenantId, context.CancellationToken);
            state.TenantSlug = RequireTenantSlug(context.Message.TenantSlug, context.Message.TenantId);
            state.TenantStatus = context.Message.Status.Trim().ToLowerInvariant();
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<TenantLicenseChangedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.TenantId, context.Message.OccurredAtUtc, async () =>
        {
            var state = await GetOrCreateAsync(context.Message.TenantId, context.CancellationToken);
            state.TenantSlug = RequireTenantSlug(context.Message.TenantSlug, context.Message.TenantId);

            if (state.Version > context.Message.Version)
                return;

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
            // Do not use Serializable here. These consumers are independent event
            // projections and the tenant row is protected by its primary key.
            // Serializable turns the get-or-create read into a range-lock hotspot
            // when Created/Status/License events arrive together.
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                db.ChangeTracker.Clear();

                // Fast idempotency check. The Inbox primary key is the final guard
                // when two deliveries of the same event race.
                if (await db.InboxMessages
                        .AsNoTracking()
                        .AnyAsync(
                            x => x.Id == eventId && x.Consumer == ConsumerName,
                            ct))
                {
                    return;
                }

                await using var transaction =
                    await db.Database.BeginTransactionAsync(
                        System.Data.IsolationLevel.ReadCommitted,
                        ct);

                // Serialize entitlement creation/update per tenant without using
                // Serializable isolation or range locks. Created/Status/License
                // events for the same tenant can arrive concurrently.
                var lockResource = $"leadsai:tenant-entitlement:{ConsumerName}:{tenantId:D}";
                await db.Database.ExecuteSqlRawAsync(
                    "EXEC sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 5000",
                    new object[] { lockResource },
                    ct);

                try
                {
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
                    return;
                }
                catch (DbUpdateException ex) when (IsDuplicateKey(ex))
                {
                    await transaction.RollbackAsync(ct);
                    db.ChangeTracker.Clear();

                    // If the inbox row now exists, another delivery completed
                    // this exact event. A duplicate TenantEntitlements insert,
                    // however, only means another event won the create race.
                    if (await db.InboxMessages
                            .AsNoTracking()
                            .AnyAsync(
                                x => x.Id == eventId && x.Consumer == ConsumerName,
                                ct))
                    {
                        return;
                    }

                    // Another event created the tenant between our read and
                    // insert. Reload the projection and apply this event again.
                    if (attempt == 3)
                        throw;

                    await Task.Delay(TimeSpan.FromMilliseconds(25 * attempt), ct);
                }
            }
        });
    }

    private static bool IsDuplicateKey(DbUpdateException ex)
        => ex.InnerException is SqlException sql
            && (sql.Number == 2601 || sql.Number == 2627);

}
