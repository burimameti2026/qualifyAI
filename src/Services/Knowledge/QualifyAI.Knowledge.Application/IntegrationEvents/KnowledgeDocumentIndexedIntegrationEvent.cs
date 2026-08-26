using QualifyAI.BuildingBlocks.Messaging;
namespace QualifyAI.Knowledge.Application.IntegrationEvents;
public sealed record KnowledgeDocumentIndexedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId, Guid AggregateId)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, CorrelationId);
