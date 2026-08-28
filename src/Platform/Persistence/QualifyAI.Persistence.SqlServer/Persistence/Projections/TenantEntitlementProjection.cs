namespace QualifyAI.Persistence.SqlServer.Projections;

public sealed class TenantEntitlementProjection
{
    public Guid TenantId { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantStatus { get; set; } = "pending";
    public string LicensePlan { get; set; } = "unassigned";
    public string LicenseStatus { get; set; } = "unassigned";
    public int MaxUsers { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public long Version { get; set; }
    public string ModulesJson { get; set; } = "[]";
    public string LimitsJson { get; set; } = "{}";
    public DateTime UpdatedAtUtc { get; set; }
}
