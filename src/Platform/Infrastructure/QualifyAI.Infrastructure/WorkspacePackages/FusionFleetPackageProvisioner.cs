using QualifyAI.Infrastructure.Demo;

namespace QualifyAI.Infrastructure.WorkspacePackages;

/// <summary>
/// Package boundary for FusionFleet Promotion provisioning. The stable combined
/// presentation scenario remains available until its existing seed blocks are
/// migrated here one by one without changing production behavior.
/// </summary>
public sealed class FusionFleetPackageProvisioner(RealisticScenarioService scenarios)
{
    public async Task<ScenarioInstallResult> ProvisionAsync(Guid tenantId, CancellationToken ct = default)
        => await scenarios.InstallAsync(tenantId, ct);
}
