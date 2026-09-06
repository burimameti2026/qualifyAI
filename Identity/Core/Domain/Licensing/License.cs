using QualifyAI.BuildingBlocks.Domain.Abstractions;

namespace QualifyAI.Identity.Domain.Licensing;

public sealed class License : AggregateRoot
{
    private readonly List<LicenseModule> _modules = [];

    private License() { }

    private License(Guid tenantId, string plan, DateTime startsAtUtc, DateTime? expiresAtUtc, DateTime? gracePeriodEndsAtUtc, int maxUsers)
    {
        TenantId = tenantId == Guid.Empty ? throw new ArgumentException("Tenant is required.", nameof(tenantId)) : tenantId;
        Plan = Require(plan, nameof(plan));
        StartsAtUtc = startsAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        GracePeriodEndsAtUtc = gracePeriodEndsAtUtc;
        MaxUsers = maxUsers > 0 ? maxUsers : throw new ArgumentOutOfRangeException(nameof(maxUsers));
        ValidateDates();
        Status = LicenseStatus.Active;
        Version = 1;
    }

    public Guid TenantId { get; private set; }
    public string Plan { get; private set; } = string.Empty;
    public LicenseStatus Status { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public DateTime? GracePeriodEndsAtUtc { get; private set; }
    public int MaxUsers { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyCollection<LicenseModule> Modules => _modules.AsReadOnly();

    public static License Create(Guid tenantId, string plan, DateTime startsAtUtc, DateTime? expiresAtUtc, int maxUsers, IEnumerable<string> modules)
        => Create(tenantId, plan, startsAtUtc, expiresAtUtc, null, maxUsers, modules);

    public static License Create(Guid tenantId, string plan, DateTime startsAtUtc, DateTime? expiresAtUtc, DateTime? gracePeriodEndsAtUtc, int maxUsers, IEnumerable<string> modules)
    {
        var license = new License(tenantId, plan, startsAtUtc, expiresAtUtc, gracePeriodEndsAtUtc, maxUsers);
        license.ReplaceModules(modules);
        return license;
    }

    public void ChangePlan(string plan, int maxUsers, DateTime? expiresAtUtc)
        => ChangePlan(plan, maxUsers, expiresAtUtc, GracePeriodEndsAtUtc);

    public void ChangePlan(string plan, int maxUsers, DateTime? expiresAtUtc, DateTime? gracePeriodEndsAtUtc)
    {
        Plan = Require(plan, nameof(plan));
        MaxUsers = maxUsers > 0 ? maxUsers : throw new ArgumentOutOfRangeException(nameof(maxUsers));
        ExpiresAtUtc = expiresAtUtc;
        GracePeriodEndsAtUtc = gracePeriodEndsAtUtc;
        ValidateDates();
        Version++;
    }

    public void Renew(DateTime startsAtUtc, DateTime? expiresAtUtc, DateTime? gracePeriodEndsAtUtc = null)
    {
        StartsAtUtc = startsAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        GracePeriodEndsAtUtc = gracePeriodEndsAtUtc;
        ValidateDates();
        Status = LicenseStatus.Active;
        Version++;
    }

    public void ReplaceModules(IEnumerable<string> modules)
    {
        var normalized = modules.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        _modules.RemoveAll(x => !normalized.Contains(x.Code, StringComparer.OrdinalIgnoreCase));
        foreach (var module in normalized.Where(x => _modules.All(m => !string.Equals(m.Code, x, StringComparison.OrdinalIgnoreCase)))) _modules.Add(LicenseModule.Create(Id, module));
        Version++;
    }

    public void Suspend() { if (Status != LicenseStatus.Cancelled) { Status = LicenseStatus.Suspended; Version++; } }
    public void Activate() { if (Status is not LicenseStatus.Cancelled) { Status = LicenseStatus.Active; Version++; } }
    public void Cancel() { if (Status != LicenseStatus.Cancelled) { Status = LicenseStatus.Cancelled; Version++; } }

    public LicenseStatus GetEffectiveStatus(DateTime utcNow)
    {
        if (Status is LicenseStatus.Suspended or LicenseStatus.Cancelled) return Status;
        if (StartsAtUtc > utcNow) return LicenseStatus.Inactive;
        if (!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value > utcNow) return Status == LicenseStatus.Trial ? LicenseStatus.Trial : LicenseStatus.Active;
        if (GracePeriodEndsAtUtc.HasValue && GracePeriodEndsAtUtc.Value > utcNow) return LicenseStatus.GracePeriod;
        return LicenseStatus.Expired;
    }

    public bool IsUsable(DateTime utcNow) => GetEffectiveStatus(utcNow) is LicenseStatus.Active or LicenseStatus.Trial or LicenseStatus.GracePeriod;

    private void ValidateDates()
    {
        if (ExpiresAtUtc.HasValue && ExpiresAtUtc.Value <= StartsAtUtc) throw new ArgumentException("Expiration must be after the license start date.", nameof(ExpiresAtUtc));
        if (GracePeriodEndsAtUtc.HasValue && (!ExpiresAtUtc.HasValue || GracePeriodEndsAtUtc.Value <= ExpiresAtUtc.Value)) throw new ArgumentException("Grace period must end after license expiration.", nameof(GracePeriodEndsAtUtc));
    }

    private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();
}

public enum LicenseStatus { Trial = 0, Active = 1, Inactive = 2, GracePeriod = 3, Suspended = 4, Expired = 5, Cancelled = 6 }
