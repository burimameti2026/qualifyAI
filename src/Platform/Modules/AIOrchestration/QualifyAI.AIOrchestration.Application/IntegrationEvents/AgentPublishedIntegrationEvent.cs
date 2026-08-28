using QualifyAI.BuildingBlocks.Messaging;
namespace QualifyAI.AIOrchestration.Application.IntegrationEvents;
public sealed record AgentPublishedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId, Guid AggregateId)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, CorrelationId);
