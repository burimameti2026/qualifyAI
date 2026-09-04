using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Automation.Domain.Events;
public sealed record AutomationTriggeredDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
