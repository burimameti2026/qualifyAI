using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Integrations.Domain.Events;
public sealed record IntegrationConnectedDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
