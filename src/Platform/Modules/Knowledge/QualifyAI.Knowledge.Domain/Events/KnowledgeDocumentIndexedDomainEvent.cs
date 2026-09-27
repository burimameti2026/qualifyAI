using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Knowledge.Domain.Events;
public sealed record KnowledgeDocumentIndexedDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
