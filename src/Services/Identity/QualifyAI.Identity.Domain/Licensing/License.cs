using QualifyAI.BuildingBlocks.Domain.Abstractions;

namespace QualifyAI.Identity.Domain.Licensing;

public sealed class License : AggregateRoot
{
    private readonly List<string> _modules = [];

    private License() { }

    private License(Guid tenantId, string plan, DateTime startsAtUtc, DateTime? expiresAtUtc, int maxUsers)
    {
        TenantId = tenantId;
        Plan = Require(plan, nameof(plan));
        StartsAtUtc = startsAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        MaxUsers = maxUsers > 0 ? maxUsers : throw new ArgumentOutOfRangeException(nameof(maxUsers));
        Status = LicenseStatus.Active;
        Version = 1;
    }

    public Guid TenantId { get; private set; }
    public string Plan { get; private set; } = string.Empty;
    public LicenseStatus Status { get; private set; }
    public DateTime StartsAtUtc { get; private set; }
    public DateTime? ExpiresAtUtc { get; private set; }
    public int MaxUsers { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyCollection<string> Modules => _modules.AsReadOnly();

    public static License Create(Guid tenantId, string plan, DateTime startsAtUtc, DateTime? expiresAtUtc, int maxUsers, IEnumerable<string> modules)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        var license = new License(tenantId, plan, startsAtUtc, expiresAtUtc, maxUsers);
        license.ReplaceModules(modules);
        return license;
    }

    public void ChangePlan(string plan, int maxUsers, DateTime? expiresAtUtc)
    {
        Plan = Require(plan, nameof(plan));
        MaxUsers = maxUsers > 0 ? maxUsers : throw new ArgumentOutOfRangeException(nameof(maxUsers));
        ExpiresAtUtc = expiresAtUtc;
        Version++;
    }

    public void ReplaceModules(IEnumerable<string> modules)
    {
        _modules.Clear();
        _modules.AddRange(modules.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));
        Version++;
    }

    public void Suspend() { Status = LicenseStatus.Suspended; Version++; }
    public void Activate() { Status = LicenseStatus.Active; Version++; }
    public void Cancel() { Status = LicenseStatus.Cancelled; Version++; }

    public bool IsUsable(DateTime utcNow)
        => Status == LicenseStatus.Active && StartsAtUtc <= utcNow && (!ExpiresAtUtc.HasValue || ExpiresAtUtc.Value > utcNow);

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Value is required.", name) : value.Trim();
}

public enum LicenseStatus
{
    Trial = 0,
    Active = 1,
    Suspended = 2,
    Expired = 3,
    Cancelled = 4
}
