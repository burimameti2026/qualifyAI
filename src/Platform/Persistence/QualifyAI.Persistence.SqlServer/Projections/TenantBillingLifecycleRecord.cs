namespace QualifyAI.Persistence.SqlServer.Projections;

public sealed class TenantBillingLifecycleRecord
{
    public Guid TenantId { get; set; }
    public string State { get; set; } = "Active";
    public DateTime? TrialEndsAtUtc { get; set; }
    public DateTime? GraceEndsAtUtc { get; set; }
    public int RetryAttempt { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public string? LastPaymentState { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
