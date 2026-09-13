using LeadsAI.BuildingBlocks.Messaging;
namespace LeadsAI.Automation.Application.IntegrationEvents;
public sealed record AutomationTriggeredIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId, Guid AggregateId)
    : IntegrationEvent(EventId, TenantId, OccurredAtUtc, CorrelationId);
