using LeadsAI.BuildingBlocks.Domain.Abstractions;
namespace LeadsAI.Integrations.Domain.Events;
public sealed record IntegrationConnectedDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
