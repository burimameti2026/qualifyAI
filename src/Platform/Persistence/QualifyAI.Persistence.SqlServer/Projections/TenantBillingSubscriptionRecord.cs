namespace QualifyAI.Persistence.SqlServer.Projections;

public sealed class TenantBillingSubscriptionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Provider { get; set; } = null!;
    public string ExternalSubscriptionId { get; set; } = null!;
    public string Plan { get; set; } = null!;
    public string Status { get; set; } = null!;
    public DateTime StartedAtUtc { get; set; }
    public DateTime? CurrentPeriodEndsAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
