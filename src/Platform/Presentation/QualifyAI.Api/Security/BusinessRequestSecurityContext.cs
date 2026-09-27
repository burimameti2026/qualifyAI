using System.Security.Claims;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.BuildingBlocks.Application.Security;
using QualifyAI.BuildingBlocks.Security.Claims;

namespace QualifyAI.Api.Security;

public sealed class BusinessRequestSecurityContext(
    IHttpContextAccessor httpContextAccessor,
    ITenantEntitlementRepository entitlements) : IRequestSecurityContext
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;

    public Guid? TenantId
        => Guid.TryParse(User.FindFirst(QualifyAiClaimTypes.TenantId)?.Value, out var id) ? id : null;

    public long? LicenseVersion
        => long.TryParse(User.FindFirst(QualifyAiClaimTypes.LicenseVersion)?.Value, out var version) ? version : null;

    public bool HasPermission(string permission)
        => User.FindAll(QualifyAiClaimTypes.Permission)
            .Any(x => string.Equals(x.Value, permission, StringComparison.OrdinalIgnoreCase))
           || User.IsInRole("platform-admin")
           || User.IsInRole("PlatformAdmin");

    public async Task<TenantAccessSnapshot?> GetEntitlementAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var snapshot = await entitlements.GetAsync(tenantId, cancellationToken);
        return snapshot is null
            ? null
            : new TenantAccessSnapshot(
                snapshot.TenantId,
                snapshot.IsAccessibleAt(DateTime.UtcNow),
                snapshot.Version,
                snapshot.EnabledModules);
    }
}
