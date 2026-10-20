using LeadsAI.BuildingBlocks.Messaging;
namespace LeadsAI.Contracts.Notifications;
public sealed record NotificationRequestedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId,
    string Channel, string Recipient, string Template, string PayloadJson)
    : IntegrationEvent(EventId,TenantId,OccurredAtUtc,CorrelationId);
