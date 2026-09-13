using LeadsAI.Infrastructure.Demo;

namespace LeadsAI.Infrastructure.WorkspacePackages;

public sealed class FusionFleetPackageProvisioner(RealisticScenarioService scenarios)
{
    public Task<ScenarioInstallResult> ProvisionAsync(Guid tenantId, CancellationToken ct = default)
        => scenarios.InstallFusionFleetPackageAsync(tenantId, ct);
}
