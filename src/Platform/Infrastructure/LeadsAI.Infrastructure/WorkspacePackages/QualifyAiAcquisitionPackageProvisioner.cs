using LeadsAI.Infrastructure.Demo;

namespace LeadsAI.Infrastructure.WorkspacePackages;

public sealed class QualifyAiAcquisitionPackageProvisioner(RealisticScenarioService scenarios)
{
    public Task<ScenarioInstallResult> ProvisionAsync(Guid tenantId, CancellationToken ct = default)
        => scenarios.InstallQualifyAiAcquisitionPackageAsync(tenantId, ct);
}
