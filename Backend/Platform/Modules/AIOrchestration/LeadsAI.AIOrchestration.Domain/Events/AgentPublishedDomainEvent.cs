using LeadsAI.BuildingBlocks.Domain.Abstractions;
namespace LeadsAI.AIOrchestration.Domain.Events;
public sealed record AgentPublishedDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
