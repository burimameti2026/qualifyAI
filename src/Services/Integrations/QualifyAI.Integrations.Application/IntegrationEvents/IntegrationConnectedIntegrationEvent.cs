using QualifyAI.BuildingBlocks.Messaging;
namespace QualifyAI.Integrations.Application.IntegrationEvents;
public sealed record IntegrationConnectedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId, Guid AggregateId)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, CorrelationId);
