namespace QualifyAI.Persistence.SqlServer.Projections;

public sealed class TenantLifecycleEventRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Type { get; set; } = null!;
    public string Status { get; set; } = null!;
    public string Message { get; set; } = null!;
    public string? DataJson { get; set; }
    public string? CorrelationId { get; set; }
    public string Source { get; set; } = "system";
    public string? ActorId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
