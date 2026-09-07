using QualifyAI.Infrastructure.Demo;

namespace QualifyAI.Infrastructure.WorkspacePackages;

/// <summary>
/// Package boundary for QualifyAI Acquisition provisioning. It is intentionally
/// isolated from the package API so its existing scenario seed can be extracted
/// independently while preserving the legacy combined installer.
/// </summary>
public sealed class QualifyAiAcquisitionPackageProvisioner(RealisticScenarioService scenarios)
{
    public async Task<ScenarioInstallResult> ProvisionAsync(Guid tenantId, CancellationToken ct = default)
        => await scenarios.InstallAsync(tenantId, ct);
}
