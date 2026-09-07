using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.Demo;

namespace QualifyAI.Api.Controllers;

public sealed record InstallWorkspacePackageRequest(string PackageId);

[ApiController]
[Authorize]
[Route("api/workspace-packages")]
public sealed class WorkspacePackagesController(ITenantContext tenant, RealisticScenarioService scenarios) : ControllerBase
{
    [HttpPost("install")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Install([FromBody] InstallWorkspacePackageRequest request, CancellationToken ct)
    {
        if (string.Equals(request.PackageId, "blank", StringComparison.OrdinalIgnoreCase))
            return Ok(new { packageId = "blank", installed = true });

        if (string.Equals(request.PackageId, "fusionfleet-promotion", StringComparison.OrdinalIgnoreCase))
            return Ok(await scenarios.InstallAsync(tenant.TenantId(), ct));

        if (string.Equals(request.PackageId, "qualifyai-acquisition", StringComparison.OrdinalIgnoreCase))
            return Ok(await scenarios.InstallAsync(tenant.TenantId(), ct));

        return BadRequest(new { detail = $"Unknown workspace package '{request.PackageId}'." });
    }

    [HttpPost("tenant/{tenantId:guid}/install")]
    [RequirePermission(QualifyAiPermissions.SystemAdmin)]
    public async Task<IActionResult> InstallForTenant(Guid tenantId, [FromBody] InstallWorkspacePackageRequest request, CancellationToken ct)
    {
        if (string.Equals(request.PackageId, "blank", StringComparison.OrdinalIgnoreCase))
            return Ok(new { packageId = "blank", tenantId, installed = true });

        if (request.PackageId is "fusionfleet-promotion" or "qualifyai-acquisition")
            return Ok(await scenarios.InstallAsync(tenantId, ct));

        return BadRequest(new { detail = $"Unknown workspace package '{request.PackageId}'." });
    }
}
