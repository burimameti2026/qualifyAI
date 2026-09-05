namespace QualifyAI.Persistence.SqlServer.Projections;

public sealed class TenantModuleProvisioning
{
    public Guid TenantId { get; set; }
    public string ModuleCode { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? NextRetryAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
