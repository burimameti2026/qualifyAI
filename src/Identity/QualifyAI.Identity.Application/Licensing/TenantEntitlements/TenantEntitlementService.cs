using QualifyAI.Identity.Domain.Licensing;

namespace QualifyAI.Identity.Application.Licensing;

public interface ITenantEntitlementService
{
    Task<TenantEntitlements> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureModuleAsync(Guid tenantId, string module, CancellationToken cancellationToken = default);
}
public sealed record TenantEntitlements(
    Guid TenantId,
    string Plan,
    string LicenseStatus,
    bool IsUsable,
    int MaxUsers,
    DateTime StartsAtUtc,
    DateTime? ExpiresAtUtc,
    long Version,
    IReadOnlyCollection<string> Modules);
//public sealed record TenantEntitlements(
//    Guid TenantId,
//    string Plan,
//    LicenseStatus Status,
//    DateTime? ExpiresAtUtc,
//    DateTime? GracePeriodEndsAtUtc,
//    int MaxUsers,
//    IReadOnlyCollection<string> Modules);
