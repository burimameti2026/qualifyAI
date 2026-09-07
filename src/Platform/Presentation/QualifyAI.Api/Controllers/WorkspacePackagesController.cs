using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.Demo;
using QualifyAI.Infrastructure.WorkspacePackages;

namespace QualifyAI.Api.Controllers;

public sealed record InstallWorkspacePackageRequest(string PackageId);

[ApiController]
[Authorize]
[Route("api/workspace-packages")]
public sealed class WorkspacePackagesController(ITenantContext tenant, RealisticScenarioService scenarios) : ControllerBase
{
    [HttpGet]
    public IActionResult List() => Ok(new[]
    {
        WorkspacePackageCatalog.FusionFleetPromotion,
        WorkspacePackageCatalog.QualifyAiAcquisition,
        WorkspacePackageCatalog.Blank
    });

    [HttpPost("install")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Install([FromBody] InstallWorkspacePackageRequest request, CancellationToken ct)
        => await InstallCore(tenant.TenantId(), request, ct);

    [HttpPost("tenant/{tenantId:guid}/install")]
    [RequirePermission(QualifyAiPermissions.SystemAdmin)]
    public async Task<IActionResult> InstallForTenant(Guid tenantId, [FromBody] InstallWorkspacePackageRequest request, CancellationToken ct)
        => await InstallCore(tenantId, request, ct);

    private async Task<IActionResult> InstallCore(Guid tenantId, InstallWorkspacePackageRequest request, CancellationToken ct)
    {
        if (!WorkspacePackageCatalog.TryGet(request.PackageId, out var package))
            return BadRequest(new { detail = $"Unknown workspace package '{request.PackageId}'." });

        if (package.Id == "blank")
            return Ok(new { packageId = package.Id, package.Name, package.Version, tenantId, installed = true, included = package.Included, message = "Blank workspace ready." });

        var existing = await scenarios.GetPackageReadinessAsync(tenantId, package.Id, ct);
        if (existing.Installed)
            return Ok(new { packageId = package.Id, package.Name, package.Version, tenantId, installed = true, alreadyInstalled = true, included = package.Included, readiness = existing });

        var result = await scenarios.InstallPackageAsync(tenantId, package.Id, ct);
        return Ok(new { packageId = package.Id, package.Name, package.Version, tenantId, installed = true, alreadyInstalled = false, included = package.Included, readiness = result });
    }
}
