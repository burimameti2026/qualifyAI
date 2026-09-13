namespace LeadsAI.BuildingBlocks.Domain.Abstractions;
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAtUtc { get; }
}
