using LeadsAI.BuildingBlocks.Messaging;
namespace LeadsAI.Knowledge.Application.IntegrationEvents;
public sealed record KnowledgeDocumentIndexedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId, Guid AggregateId)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, CorrelationId);
