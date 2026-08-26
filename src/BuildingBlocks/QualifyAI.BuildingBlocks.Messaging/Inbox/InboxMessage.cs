namespace QualifyAI.BuildingBlocks.Messaging.Inbox;
public sealed class InboxMessage
{
    public Guid Id { get; set; }
    public string Consumer { get; set; } = "";
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
}
