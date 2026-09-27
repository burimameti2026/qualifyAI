using QualifyAI.Application.Entitlements;

namespace QualifyAI.Application.Abstractions.Persistence;

public interface ITenantEntitlementRepository
{
    Task<TenantEntitlementSnapshot?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<TenantEntitlementSnapshot?> FindActiveBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ListActiveTenantIdsAsync(CancellationToken cancellationToken = default);

    Task<TenantEntitlementSnapshot> ProvisionFromSignedTokenAsync(
        Guid tenantId,
        string tenantSlug,
        string plan,
        string licenseStatus,
        long version,
        IReadOnlyCollection<string> modules,
        DateTime? tokenExpiresAtUtc,
        CancellationToken cancellationToken = default);

    Task UpsertTenantAsync(
        Guid tenantId,
        string tenantSlug,
        string tenantStatus,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);

    Task UpsertLicenseAsync(
        Guid tenantId,
        string plan,
        string licenseStatus,
        int maxUsers,
        DateTime startsAtUtc,
        DateTime? expiresAtUtc,
        long version,
        IReadOnlyCollection<string> modules,
        IReadOnlyDictionary<string, int>? limits,
        DateTime updatedAtUtc,
        CancellationToken cancellationToken = default);
}
