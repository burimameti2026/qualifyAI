using QualifyAI.BuildingBlocks.Messaging;
namespace QualifyAI.Contracts.AI;
public sealed record IntentDetectedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId,
    Guid ConversationId, string Intent, decimal Confidence)
    : IntegrationEvent(EventId,TenantId,OccurredAtUtc,CorrelationId);
