namespace QualifyAI.Persistence.SqlServer.Projections;

public sealed class BillingEventRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Provider { get; set; } = null!;
    public string ExternalEventId { get; set; } = null!;
    public string Type { get; set; } = null!;
    public Guid TenantId { get; set; }
    public string Status { get; set; } = null!;
    public string? DataJson { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}
