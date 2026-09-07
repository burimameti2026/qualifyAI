using QualifyAI.Infrastructure.Demo;

namespace QualifyAI.Infrastructure.WorkspacePackages;

public sealed class QualifyAiAcquisitionPackageProvisioner(RealisticScenarioService scenarios)
{
    public Task<ScenarioInstallResult> ProvisionAsync(Guid tenantId, CancellationToken ct = default)
        => scenarios.InstallQualifyAiAcquisitionPackageAsync(tenantId, ct);
}
