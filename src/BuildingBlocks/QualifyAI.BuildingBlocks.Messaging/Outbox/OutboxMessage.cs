namespace QualifyAI.BuildingBlocks.Messaging.Outbox;
public sealed class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public string Type { get; set; } = "";
    public string Payload { get; set; } = "";
    public DateTime? ProcessedAtUtc { get; set; }
    public string? Error { get; set; }
}
