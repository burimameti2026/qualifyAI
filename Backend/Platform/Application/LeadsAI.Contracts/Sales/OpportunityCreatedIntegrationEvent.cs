using LeadsAI.BuildingBlocks.Messaging;
namespace LeadsAI.Contracts.Sales;
public sealed record OpportunityCreatedIntegrationEvent(
    Guid EventId, Guid TenantId, DateTime OccurredAtUtc, Guid CorrelationId,
    Guid OpportunityId, Guid? LeadId, decimal Value)
    : IntegrationEvent(EventId,TenantId,OccurredAtUtc,CorrelationId);
