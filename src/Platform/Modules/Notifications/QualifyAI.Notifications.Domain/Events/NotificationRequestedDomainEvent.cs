using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Notifications.Domain.Events;
public sealed record NotificationRequestedDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
