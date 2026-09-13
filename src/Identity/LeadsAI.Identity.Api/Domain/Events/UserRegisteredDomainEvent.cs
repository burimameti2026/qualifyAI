using LeadsAI.BuildingBlocks.Domain.Abstractions;
namespace LeadsAI.Identity.Domain.Events;
public sealed record UserRegisteredDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
