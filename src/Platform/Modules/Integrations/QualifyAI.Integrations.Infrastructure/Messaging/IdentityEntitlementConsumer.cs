using System.Data;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.Entitlements;
using QualifyAI.BuildingBlocks.Messaging.Inbox;
using QualifyAI.Contracts.Identity;
using QualifyAI.Integrations.Persistence.SqlServer;

namespace QualifyAI.Integrations.Infrastructure.Messaging;

public sealed class IdentityEntitlementConsumer(IntegrationsDbContext db) :
    IConsumer<TenantCreatedIntegrationEvent>,
    IConsumer<TenantStatusChangedIntegrationEvent>,
    IConsumer<TenantLicenseChangedIntegrationEvent>
{
    private const string ConsumerName = "integrations.identity-entitlements";

    public Task Consume(ConsumeContext<TenantCreatedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.OccurredAtUtc, async () =>
        {
            var state = await GetOrCreateAsync(context.Message.TenantId, context.CancellationToken);
            state.TenantSlug = context.Message.TenantSlug.Trim().ToLowerInvariant();
            state.TenantStatus = "active";
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<TenantStatusChangedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.OccurredAtUtc, async () =>
        {
            var state = await GetOrCreateAsync(context.Message.TenantId, context.CancellationToken);
            state.TenantSlug = context.Message.TenantSlug.Trim().ToLowerInvariant();
            state.TenantStatus = context.Message.Status.Trim().ToLowerInvariant();
        }, context.CancellationToken);

    public Task Consume(ConsumeContext<TenantLicenseChangedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.OccurredAtUtc, async () =>
        {
            var state = await GetOrCreateAsync(context.Message.TenantId, context.CancellationToken);
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
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.TenantEntitlements WITH (UPDLOCK, HOLDLOCK)
                WHERE TenantId = {tenantId}
            )
            BEGIN
                INSERT INTO dbo.TenantEntitlements (TenantId)
                VALUES ({tenantId})
            END
            """, ct);

        return await db.TenantEntitlements.FirstAsync(x => x.TenantId == tenantId, ct);
    }

    private async Task ProcessAsync(Guid eventId, DateTime occurredAtUtc, Func<Task> mutate, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            if (await db.InboxMessages.AnyAsync(
                    x => x.Id == eventId && x.Consumer == ConsumerName,
                    ct))
            {
                await transaction.CommitAsync(ct);
                return;
            }

            await mutate();

            db.InboxMessages.Add(new InboxMessage
            {
                Id = eventId,
                Consumer = ConsumerName,
                ReceivedAtUtc = DateTime.UtcNow,
                ProcessedAtUtc = DateTime.UtcNow
            });

            var tracked = db.ChangeTracker.Entries<TenantEntitlementState>()
                .FirstOrDefault(x => x.State != EntityState.Unchanged)?.Entity;

            if (tracked is not null)
                tracked.UpdatedAtUtc = occurredAtUtc;

            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
    }
}
