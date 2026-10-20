using LeadsAI.BuildingBlocks.Domain.Abstractions;
namespace LeadsAI.Knowledge.Domain.Events;
public sealed record KnowledgeDocumentIndexedDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
