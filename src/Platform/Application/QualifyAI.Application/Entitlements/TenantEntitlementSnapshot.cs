namespace QualifyAI.Application.Entitlements;

public sealed record TenantEntitlementSnapshot(
    Guid TenantId,
    string TenantSlug,
    string TenantStatus,
    string LicensePlan,
    string LicenseStatus,
    int MaxUsers,
    DateTime StartsAtUtc,
    DateTime? ExpiresAtUtc,
    long Version,
    IReadOnlyList<string> EnabledModules,
    IReadOnlyDictionary<string, int> Limits,
    DateTime UpdatedAtUtc)
{
    public bool IsAccessibleAt(DateTime utcNow)
        => string.Equals(TenantStatus, "active", StringComparison.OrdinalIgnoreCase)
           && string.Equals(LicenseStatus, "active", StringComparison.OrdinalIgnoreCase)
           && StartsAtUtc <= utcNow
           && (!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value > utcNow);

    public bool HasModule(string module)
        => EnabledModules.Any(x => string.Equals(x, module, StringComparison.OrdinalIgnoreCase));
}
