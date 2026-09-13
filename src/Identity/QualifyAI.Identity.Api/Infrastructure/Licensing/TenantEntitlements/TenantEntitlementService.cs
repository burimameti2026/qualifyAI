using Microsoft.EntityFrameworkCore;
using QualifyAI.Identity.Application;
using QualifyAI.Identity.Application.Licensing;
using QualifyAI.Identity.Domain.Licensing;
using QualifyAI.Identity.Persistence.SqlServer;

namespace QualifyAI.Identity.Infrastructure.Licensing;

public sealed class TenantEntitlementService(IdentityDbContext db) : ITenantEntitlementService
{
    public async Task<TenantEntitlements> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var license = await db.Licenses
            .AsNoTracking()
            .Include(x => x.Modules)
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken)
            ?? throw new IdentityValidationException("license", "Tenant does not have a license.");

        var status = license.GetEffectiveStatus(DateTime.UtcNow);
        var modules = license.Modules
            .Where(x => x.Enabled)
            .Select(x => x.Code)
            .OrderBy(x => x)
            .ToArray();

        return new TenantEntitlements(
          license.TenantId,
          license.Plan,
          license.Status.ToString(),
          license.IsUsable(DateTime.UtcNow),
          license.MaxUsers,
          license.StartsAtUtc,
          license.ExpiresAtUtc,
          license.Version,
          license.Modules.Where(x => x.Enabled).Select(x => x.Code).ToArray());
    }

    public async Task EnsureModuleAsync(Guid tenantId, string module, CancellationToken cancellationToken = default)
    {
        var entitlements = await GetAsync(tenantId, cancellationToken);
        if (entitlements.LicenseStatus is not ("Active" or "Trial" or "GracePeriod"))
            throw new IdentityValidationException("license", $"Tenant license is {entitlements.LicenseStatus}.");

        var normalized = module.Trim().ToLowerInvariant();
        if (!entitlements.Modules.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            throw new IdentityValidationException("module", $"Module '{normalized}' is not included in the current license.");
    }
}
