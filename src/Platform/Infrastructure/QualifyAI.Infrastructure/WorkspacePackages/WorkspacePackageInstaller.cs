using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Demo;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.WorkspacePackages;

public sealed record WorkspacePackageInstallResult(string PackageId, string Scenario, int Prospects, int Campaigns, int Opportunities, int Meetings, int Tickets, int Automations);

public sealed class WorkspacePackageInstaller(AppDbContext db, RealisticScenarioService scenarios)
{
    public async Task<WorkspacePackageInstallResult> InstallAsync(Guid tenantId, string packageId, CancellationToken ct = default)
    {
        if (!WorkspacePackageCatalog.TryGet(packageId, out var package))
            throw new InvalidOperationException($"Unknown workspace package '{packageId}'.");

        if (package.Id == "blank")
            return await SnapshotAsync(tenantId, package.Id, "Blank workspace", ct);

        if (package.Id == "fusionfleet-promotion")
        {
            var result = await scenarios.InstallAsync(tenantId, ct);
            return new WorkspacePackageInstallResult(package.Id, "FusionFleet Promotion", result.Prospects, result.Campaigns, result.Opportunities, result.Meetings, result.Tickets, result.Automations);
        }

        if (package.Id == "qualifyai-acquisition")
        {
            var result = await scenarios.InstallAsync(tenantId, ct);
            return new WorkspacePackageInstallResult(package.Id, "QualifyAI Acquisition", result.Prospects, result.Campaigns, result.Opportunities, result.Meetings, result.Tickets, result.Automations);
        }

        throw new InvalidOperationException($"Unsupported workspace package '{packageId}'.");
    }

    private async Task<WorkspacePackageInstallResult> SnapshotAsync(Guid tenantId, string packageId, string scenario, CancellationToken ct)
        => new(
            packageId,
            scenario,
            await db.Prospects.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Campaigns.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Opportunitys.CountAsync(x => x.TenantId == tenantId, ct),
            await db.MeetingBookings.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Tickets.CountAsync(x => x.TenantId == tenantId, ct),
            await db.AutomationRules.CountAsync(x => x.TenantId == tenantId, ct));
}
