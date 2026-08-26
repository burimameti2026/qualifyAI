using QualifyAI.BuildingBlocks.Messaging;
namespace QualifyAI.Contracts.Sales;
public sealed record OpportunityCreatedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId,
    Guid OpportunityId, Guid? LeadId, decimal Value)
    : IntegrationEvent(EventId,TenantId,OccurredAtUtc,CorrelationId);
