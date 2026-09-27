namespace QualifyAI.BuildingBlocks.Messaging.Entitlements;

public sealed class TenantEntitlementState
{
    public Guid TenantId { get; set; }
    public string TenantSlug { get; set; } = string.Empty;
    public string TenantStatus { get; set; } = "pending";
    public string LicensePlan { get; set; } = "unassigned";
    public string LicenseStatus { get; set; } = "unassigned";
    public int MaxUsers { get; set; }
    public DateTime StartsAtUtc { get; set; } = DateTime.MinValue;
    public DateTime? ExpiresAtUtc { get; set; }
    public long Version { get; set; }
    public string ModulesJson { get; set; } = "[]";
    public DateTime UpdatedAtUtc { get; set; }

    public bool IsAccessibleAt(DateTime utcNow)
        => string.Equals(TenantStatus, "active", StringComparison.OrdinalIgnoreCase)
           && string.Equals(LicenseStatus, "active", StringComparison.OrdinalIgnoreCase)
           && StartsAtUtc <= utcNow
           && (!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value > utcNow);
}
