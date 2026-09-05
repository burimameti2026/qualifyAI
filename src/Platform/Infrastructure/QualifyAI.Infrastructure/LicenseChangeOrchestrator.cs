using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure;

public sealed record LicenseChangeResult(Guid TenantId, IReadOnlyCollection<string> AddedModules, IReadOnlyCollection<string> RemovedModules, IReadOnlyCollection<string> ProvisionedModules);
public interface ILicenseChangeOrchestrator { Task<LicenseChangeResult> ReconcileAsync(Guid tenantId, CancellationToken cancellationToken = default); }

public sealed class LicenseChangeOrchestrator(AppDbContext db, IModuleRegistry registry, IModuleProvisioningOrchestrator provisioning) : ILicenseChangeOrchestrator
{
    public async Task<LicenseChangeResult> ReconcileAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entitlement = await db.TenantEntitlements.SingleOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken) ?? throw new InvalidOperationException($"Tenant {tenantId} has no entitlements.");
        var entitled = (JsonSerializer.Deserialize<string[]>(entitlement.ModulesJson) ?? Array.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resolved = registry.Resolve(entitled.ToArray()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await db.TenantModuleProvisionings.Where(x => x.TenantId == tenantId).ToListAsync(cancellationToken);
        var existingCodes = existing.Select(x => x.ModuleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = resolved.Where(x => !existingCodes.Contains(x)).OrderBy(x => x).ToArray();
        var removed = existingCodes.Where(x => !resolved.Contains(x)).OrderBy(x => x).ToArray();
        foreach (var row in existing.Where(x => removed.Contains(x.ModuleCode, StringComparer.OrdinalIgnoreCase)))
        { row.Status = "deactivated"; row.NextRetryAtUtc = null; row.UpdatedAtUtc = DateTime.UtcNow; }
        if (removed.Length > 0) await db.SaveChangesAsync(cancellationToken);
        if (added.Length > 0) await provisioning.ProvisionAsync(tenantId, added, cancellationToken);
        return new LicenseChangeResult(tenantId, added, removed, added);
    }
}
