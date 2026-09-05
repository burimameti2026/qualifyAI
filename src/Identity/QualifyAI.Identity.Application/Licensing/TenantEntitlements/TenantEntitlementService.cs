using QualifyAI.Identity.Domain.Licensing;

namespace QualifyAI.Identity.Application.Licensing.TenantEntitlements;

public interface ITenantEntitlementService
{
    Task<TenantEntitlements> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task EnsureModuleAsync(Guid tenantId, string module, CancellationToken cancellationToken = default);
}

public sealed record TenantEntitlements(
    Guid TenantId,
    string Plan,
    LicenseStatus Status,
    DateTime? ExpiresAtUtc,
    DateTime? GracePeriodEndsAtUtc,
    int MaxUsers,
    IReadOnlyCollection<string> Modules);
