using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.AIOrchestration.Domain.Events;
public sealed record AgentPublishedDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
