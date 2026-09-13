using LeadsAI.BuildingBlocks.Messaging;
namespace LeadsAI.Contracts.Conversations;
public sealed record MessageReceivedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId,
    Guid ConversationId, Guid ContactId, string Channel, string Text)
    : IntegrationEvent(EventId,TenantId,OccurredAtUtc,CorrelationId);
