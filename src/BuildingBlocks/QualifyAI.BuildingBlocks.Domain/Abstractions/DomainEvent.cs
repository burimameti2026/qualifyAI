namespace QualifyAI.BuildingBlocks.Domain.Abstractions;
public abstract record DomainEvent(Guid EventId, DateTime OccurredAtUtc) : IDomainEvent;
