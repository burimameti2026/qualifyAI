using QualifyAI.BuildingBlocks.Messaging;
namespace QualifyAI.Notifications.Application.IntegrationEvents;
public sealed record NotificationRequestedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId, Guid AggregateId)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, CorrelationId);
