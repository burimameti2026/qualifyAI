using LeadsAI.BuildingBlocks.Messaging;
namespace LeadsAI.Contracts.AI;
public sealed record IntentDetectedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId,
    Guid ConversationId, string Intent, decimal Confidence)
    : IntegrationEvent(EventId,TenantId,OccurredAtUtc,CorrelationId);
