using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;
using QualifyAI.Persistence.SqlServer.Projections;

namespace QualifyAI.Infrastructure;

public sealed record TenantLifecycleRequest(Guid TenantId, IReadOnlyCollection<string> Modules);
public sealed record TenantLifecycleResult(Guid TenantId, string Status, IReadOnlyCollection<string> Modules, IReadOnlyCollection<string> FailedModules);

public interface ITenantLifecycleOrchestrator
{
    Task<TenantLifecycleResult> ActivateAsync(TenantLifecycleRequest request, CancellationToken cancellationToken = default);
}

public sealed class TenantLifecycleOrchestrator(AppDbContext db, IModuleRegistry modules, IModuleProvisioningOrchestrator provisioning) : ITenantLifecycleOrchestrator
{
    public async Task<TenantLifecycleResult> ActivateAsync(TenantLifecycleRequest request, CancellationToken cancellationToken = default)
    {
        var tenant = await db.Tenants.SingleOrDefaultAsync(x => x.Id == request.TenantId, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant {request.TenantId} was not found.");

        var resolved = modules.Resolve(request.Modules);
        var entitlement = await db.TenantEntitlements.SingleOrDefaultAsync(x => x.TenantId == request.TenantId, cancellationToken);
        if (entitlement is null)
            throw new InvalidOperationException($"Tenant {request.TenantId} has no entitlement projection.");

        entitlement.TenantStatus = "provisioning";
        await db.SaveChangesAsync(cancellationToken);

        await provisioning.ProvisionAsync(request.TenantId, resolved, cancellationToken);

        var rows = await db.TenantModuleProvisionings.Where(x => x.TenantId == request.TenantId && resolved.Contains(x.ModuleCode)).ToListAsync(cancellationToken);
        var failed = rows.Where(x => !x.Status.Equals("completed", StringComparison.OrdinalIgnoreCase)).Select(x => x.ModuleCode).ToArray();

        if (failed.Length == 0)
        {
            entitlement.TenantStatus = "active";
            await db.SaveChangesAsync(cancellationToken);
            return new TenantLifecycleResult(request.TenantId, "active", resolved, Array.Empty<string>());
        }

        entitlement.TenantStatus = "provisioning_failed";
        await db.SaveChangesAsync(cancellationToken);
        return new TenantLifecycleResult(request.TenantId, "provisioning_failed", resolved, failed);
    }
}
