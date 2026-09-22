using System.Data;
using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.AIOrchestration.Persistence.SqlServer;
using QualifyAI.BuildingBlocks.Messaging.Entitlements;
using QualifyAI.BuildingBlocks.Messaging.Inbox;
using QualifyAI.Contracts.Identity;

namespace QualifyAI.AIOrchestration.Infrastructure.Messaging;

public sealed class IdentityEntitlementConsumer(AIOrchestrationDbContext db) :
    IConsumer<TenantCreatedIntegrationEvent>,
    IConsumer<TenantStatusChangedIntegrationEvent>,
    IConsumer<TenantLicenseChangedIntegrationEvent>
{
    private const string ConsumerName = "ai-orchestration.identity-entitlements";

    public Task Consume(ConsumeContext<TenantCreatedIntegrationEvent> context)
        => ProcessAsync(context.Message.EventId, context.Message.OccurredAtUtc, async () =>
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

            await using var transaction =
                await db.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    ct);

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
