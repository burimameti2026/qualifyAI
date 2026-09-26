using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Persistence.SqlServer;
using LeadsAI.Persistence.SqlServer.Projections;

namespace LeadsAI.Infrastructure;

public sealed record TenantLifecycleRequest(Guid TenantId, IReadOnlyCollection<string> Modules);
public sealed record TenantLifecycleResult(Guid TenantId, string Status, IReadOnlyCollection<string> Modules, IReadOnlyCollection<string> FailedModules);

public sealed record LicenseChangeResult(Guid TenantId, IReadOnlyCollection<string> AddedModules, IReadOnlyCollection<string> RemovedModules, IReadOnlyCollection<string> ProvisionedModules);
public interface ILicenseChangeOrchestrator
{
    Task<LicenseChangeResult> ReconcileAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class LicenseChangeOrchestrator(AppDbContext db, IModuleRegistry registry, IModuleProvisioningOrchestrator provisioning, IModuleDeactivationOrchestrator deactivation) : ILicenseChangeOrchestrator
{
    public async Task<LicenseChangeResult> ReconcileAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        // IMPORTANT: this can run in the same unit of work as UpsertTenantAsync/UpsertLicenseAsync,
        // called *before* SaveChangesAsync. A plain tracked query against the DbSet always hits the
        // database and won't see an entity that was only Add()'d/mutated in this context and never
        // saved — so a brand-new tenant's entitlement (created moments earlier in the same call)
        // would incorrectly appear missing. Check the local ChangeTracker first.
        var entitlement = FindTrackedEntitlement(tenantId)
            ??await db.TenantEntitlements.SingleOrDefaultAsync(x => x.TenantId==tenantId, cancellationToken)
            ??throw new InvalidOperationException($"Tenant {tenantId} has no entitlements.");

        var entitled = (JsonSerializer.Deserialize<string[]>(entitlement.ModulesJson)??Array.Empty<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resolved = registry.Resolve(entitled.ToArray()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var existing = await db.TenantModuleProvisionings.Where(x => x.TenantId==tenantId).ToListAsync(cancellationToken);
        var existingCodes = existing.Select(x => x.ModuleCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = resolved.Where(x => !existingCodes.Contains(x)).OrderBy(x => x).ToArray();
        var removed = existingCodes.Where(x => !resolved.Contains(x)).OrderBy(x => x).ToArray();
        if (entitlement.LicenseStatus.Equals("active", StringComparison.OrdinalIgnoreCase))
            entitlement.TenantStatus = "provisioning";

        if(removed.Length>0)
            await deactivation.DeactivateAsync(tenantId, removed, cancellationToken);

        try
        {
            if (added.Length > 0)
                await provisioning.ProvisionAsync(tenantId, added, cancellationToken);
        }
        catch
        {
            if (entitlement.LicenseStatus.Equals("active", StringComparison.OrdinalIgnoreCase))
                entitlement.TenantStatus = "provisioning_failed";

            await db.SaveChangesAsync(cancellationToken);
            throw;
        }

        var requiredRows = await db.TenantModuleProvisionings
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && resolved.Contains(x.ModuleCode))
            .ToListAsync(cancellationToken);

        var failed = requiredRows
            .Where(x => !x.Status.Equals("completed", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.ModuleCode)
            .ToArray();

        if (entitlement.LicenseStatus.Equals("active", StringComparison.OrdinalIgnoreCase))
            entitlement.TenantStatus = failed.Length == 0 ? "active" : "provisioning_failed";

        await db.SaveChangesAsync(cancellationToken);
        return new LicenseChangeResult(tenantId, added, removed, added);
    }

    private TenantEntitlementProjection? FindTrackedEntitlement(Guid tenantId)
        => db.ChangeTracker.Entries<TenantEntitlementProjection>()
            .Select(e => e.Entity)
            .FirstOrDefault(e => e.TenantId==tenantId);
}