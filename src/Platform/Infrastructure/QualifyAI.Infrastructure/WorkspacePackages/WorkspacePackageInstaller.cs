using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.WorkspacePackages;

public sealed record WorkspacePackageInstallResult(string PackageId, string Scenario, int Prospects, int Campaigns, int Opportunities, int Meetings, int Tickets, int Automations);

public sealed class WorkspacePackageInstaller(
    AppDbContext db,
    FusionFleetPackageProvisioner fusionFleet,
    QualifyAiAcquisitionPackageProvisioner qualifyAi)
{
    public Task<WorkspacePackageInstallResult> InstallAsync(Guid tenantId, string packageId, CancellationToken ct = default)
    {
        if (!WorkspacePackageCatalog.TryGet(packageId, out var package))
            throw new InvalidOperationException($"Unknown workspace package '{packageId}'.");

        return package.Id switch
        {
            "fusionfleet-promotion" => InstallFusionFleetPackageAsync(tenantId, ct),
            "qualifyai-acquisition" => InstallQualifyAiAcquisitionPackageAsync(tenantId, ct),
            "blank" => SnapshotAsync(tenantId, package.Id, "Blank workspace", ct),
            _ => throw new InvalidOperationException($"Unsupported workspace package '{packageId}'.")
        };
    }

    private async Task<WorkspacePackageInstallResult> InstallFusionFleetPackageAsync(Guid tenantId, CancellationToken ct)
    {
        var result = await fusionFleet.ProvisionAsync(tenantId, ct);
        return new WorkspacePackageInstallResult("fusionfleet-promotion", "FusionFleet Promotion", result.Prospects, result.Campaigns, result.Opportunities, result.Meetings, result.Tickets, result.Automations);
    }

    private async Task<WorkspacePackageInstallResult> InstallQualifyAiAcquisitionPackageAsync(Guid tenantId, CancellationToken ct)
    {
        var result = await qualifyAi.ProvisionAsync(tenantId, ct);
        return new WorkspacePackageInstallResult("qualifyai-acquisition", "QualifyAI Acquisition", result.Prospects, result.Campaigns, result.Opportunities, result.Meetings, result.Tickets, result.Automations);
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
