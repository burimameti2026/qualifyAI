using LeadsAI.Domain;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.WorkspacePackages;

public sealed record WorkspacePackageInstallResult(string PackageId, string Scenario, int Prospects, int Campaigns, int Opportunities, int Meetings, int Tickets, int Automations);

public sealed class WorkspacePackageInstaller(
    AppDbContext db,
    FusionFleetPackageProvisioner fusionFleet,
    QualifyAiAcquisitionPackageProvisioner qualifyAi,
    IModuleProvisioningOrchestrator moduleProvisioning,
    IModuleRegistry moduleRegistry)
{
    public Task<WorkspacePackageInstallResult> InstallAsync(Guid tenantId, string packageId, CancellationToken ct = default)
    {
        if (!WorkspacePackageCatalog.TryGet(packageId, out var package))
            throw new InvalidOperationException($"Unknown workspace package '{packageId}'.");

        return package.Id switch
        {
            "fusionfleet-promotion" => InstallFusionFleetPackageAsync(tenantId, ct),
            "leadsai-acquisition" => InstallQualifyAiAcquisitionPackageAsync(tenantId, ct),
            "blank" => InstallProfileAsync(tenantId, package, ct),
            _ when package.ProvisioningMode.Equals("profile", StringComparison.OrdinalIgnoreCase) => InstallProfileAsync(tenantId, package, ct),
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
        return new WorkspacePackageInstallResult("leadsai-acquisition", "LeadsAI Acquisition", result.Prospects, result.Campaigns, result.Opportunities, result.Meetings, result.Tickets, result.Automations);
    }


    private async Task<WorkspacePackageInstallResult> InstallProfileAsync(Guid tenantId, WorkspacePackageDefinition package, CancellationToken ct)
    {
        var resolvedModules = moduleRegistry.Resolve(package.RequiredModules);
        var unsupported = package.RequiredModules
            .Where(code => !resolvedModules.Contains(code, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        if (unsupported.Length > 0)
            throw new InvalidOperationException($"Package '{package.Id}' requires unsupported modules: {string.Join(", ", unsupported)}.");

        await moduleProvisioning.ProvisionAsync(tenantId, resolvedModules, ct);

        const string key = "workspace.installed-package";
        var existing = await db.TenantSettings.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Key == key, ct);

        var value = System.Text.Json.JsonSerializer.Serialize(new
        {
            packageId = package.Id,
            package.Name,
            package.Version,
            category = package.Category,
            capabilities = package.Capabilities ?? Array.Empty<string>(),
            workflows = package.Workflows ?? Array.Empty<string>(),
            agents = package.Agents ?? Array.Empty<string>(),
            installedAtUtc = DateTime.UtcNow,
            included = package.Included
        });

        if (existing is null)
            db.TenantSettings.Add(new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = key, Value = value });
        else
        {
            existing.Value = value;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return await SnapshotAsync(tenantId, package.Id, $"{package.Name} profile", ct);
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
