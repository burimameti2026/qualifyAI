using LeadsAI.BuildingBlocks.Domain.Abstractions;
namespace LeadsAI.Automation.Domain.Events;
public sealed record AutomationTriggeredDomainEvent(Guid EventId, DateTime OccurredAtUtc, Guid TenantId, Guid AggregateId)
    : DomainEvent(EventId, OccurredAtUtc);
