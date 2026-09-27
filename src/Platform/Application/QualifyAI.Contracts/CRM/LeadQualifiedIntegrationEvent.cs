using QualifyAI.BuildingBlocks.Messaging;
namespace QualifyAI.Contracts.CRM;
public sealed record LeadQualifiedIntegrationEvent(
    Guid EventId,
    Guid TenantId,
    DateTime OccurredAtUtc,
    Guid CorrelationId,
    Guid LeadId,
    int Score,
    string Reason)
    : IntegrationEvent(EventId,TenantId,OccurredAtUtc,CorrelationId);
