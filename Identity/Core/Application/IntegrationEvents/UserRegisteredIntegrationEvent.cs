using QualifyAI.BuildingBlocks.Messaging;
namespace QualifyAI.Identity.Application.IntegrationEvents;
public sealed record UserRegisteredIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId, Guid AggregateId)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, CorrelationId);
