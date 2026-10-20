using LeadsAI.BuildingBlocks.Domain.Abstractions;
namespace LeadsAI.Notifications.Domain.Events;
public sealed record NotificationRequestedDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
