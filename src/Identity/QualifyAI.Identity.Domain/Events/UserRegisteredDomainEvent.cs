using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Identity.Domain.Events;
public sealed record UserRegisteredDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
