using LeadsAI.Domain;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.WorkspacePackages;

public sealed record WorkspacePackageInstallResult(
    string PackageId,
    string Scenario,
    int Prospects,
    int Campaigns,
    int Opportunities,
    int Meetings,
    int Tickets,
    int Automations,
    bool AlreadyInstalled);

public sealed class WorkspacePackageInstaller(
    AppDbContext db,
    IModuleProvisioningOrchestrator moduleProvisioning,
    IModuleRegistry moduleRegistry,
    OperationalPackageProvisioner operationalProvisioner)
{
    public Task<WorkspacePackageInstallResult> InstallAsync(
        Guid tenantId,
        string packageId,
        CancellationToken ct = default)
    {
        if (!WorkspacePackageCatalog.TryGet(packageId, out var package))
            throw new InvalidOperationException($"Unknown workspace package '{packageId}'.");

        if (package.ProvisioningMode.Equals("customer-scenario", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Workspace package '{package.Id}' is an acquisition scenario. " +
                "Provision acquisition campaigns through IndustryPackProvisioner.");

        return InstallProfileAsync(tenantId, package, ct);
    }

    private async Task<WorkspacePackageInstallResult> InstallProfileAsync(
        Guid tenantId,
        WorkspacePackageDefinition package,
        CancellationToken ct)
    {
        var resolvedModules = moduleRegistry.Resolve(package.RequiredModules);
        var unsupported = package.RequiredModules
            .Where(code => !resolvedModules.Contains(code, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (unsupported.Length > 0)
            throw new InvalidOperationException(
                $"Package '{package.Id}' requires unsupported modules: {string.Join(", ", unsupported)}.");

        await moduleProvisioning.ProvisionAsync(tenantId, resolvedModules, ct);
        await operationalProvisioner.ProvisionAsync(tenantId, package, ct);

        const string key = "workspace.installed-package";
        var existing = await db.TenantSettings.SingleOrDefaultAsync(
            x => x.TenantId == tenantId && x.Key == key, ct);

        var alreadyInstalled = false;
        if (existing is not null && !string.IsNullOrWhiteSpace(existing.Value))
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(existing.Value);
                alreadyInstalled = document.RootElement.TryGetProperty("packageId", out var packageIdElement) &&
                                   string.Equals(packageIdElement.GetString(), package.Id, StringComparison.OrdinalIgnoreCase);
            }
            catch (System.Text.Json.JsonException)
            {
                // Treat malformed legacy state as not installed and repair it below.
            }
        }

        var value = System.Text.Json.JsonSerializer.Serialize(new
        {
            packageId = package.Id,
            package.Name,
            package.Version,
            category = package.Category,
            provisioningMode = package.ProvisioningMode,
            modules = resolvedModules,
            capabilities = package.Capabilities ?? Array.Empty<string>(),
            workflows = package.Workflows ?? Array.Empty<string>(),
            agents = package.Agents ?? Array.Empty<string>(),
            installedAtUtc = DateTime.UtcNow,
            included = package.Included
        });

        if (existing is null)
            db.TenantSettings.Add(new TenantSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Key = key,
                Value = value
            });
        else
        {
            existing.Value = value;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return await SnapshotAsync(tenantId, package.Id, $"{package.Name} profile", ct);
    }

    private async Task<WorkspacePackageInstallResult> SnapshotAsync(
        Guid tenantId,
        string packageId,
        string scenario,
        CancellationToken ct)
        => new(
            packageId,
            scenario,
            await db.Prospects.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Campaigns.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Opportunitys.CountAsync(x => x.TenantId == tenantId, ct),
            await db.MeetingBookings.CountAsync(x => x.TenantId == tenantId, ct),
            await db.Tickets.CountAsync(x => x.TenantId == tenantId, ct),
            await db.AutomationRules.CountAsync(x => x.TenantId == tenantId, ct),
            alreadyInstalled);
}
