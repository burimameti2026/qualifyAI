using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Infrastructure.WorkspacePackages;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/real-workspace/legacy")]
public sealed class RealWorkspaceController(ITenantContext tenant, RealWorkspaceService workspaces) : ControllerBase
{
    [HttpGet("options")]
    public RealWorkspaceOptions Options() => workspaces.Options();

    [HttpGet]
    public Task<RealWorkspaceDraft?> Get(CancellationToken ct) => workspaces.GetAsync(tenant.TenantId(), ct);

    [HttpPost("prepare")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public async Task<IActionResult> Prepare([FromBody] PrepareRealWorkspaceRequest request, CancellationToken ct)
    {
        try { return Ok(await workspaces.PrepareAsync(tenant.TenantId(), request, ct)); }
        catch (InvalidOperationException exception) { return BadRequest(new { detail = exception.Message }); }
    }

    [HttpPut]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public async Task<IActionResult> Save([FromBody] SaveRealWorkspaceRequest request, CancellationToken ct)
    {
        try { return Ok(await workspaces.SaveAsync(tenant.TenantId(), request, ct)); }
        catch (InvalidOperationException exception) { return BadRequest(new { detail = exception.Message }); }
    }

    [HttpPost("activate")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public async Task<IActionResult> Activate([FromBody] Guid workspaceId, CancellationToken ct)
    {
        try { return Ok(await workspaces.ActivateAsync(tenant.TenantId(), workspaceId, ct)); }
        catch (InvalidOperationException exception) { return BadRequest(new { detail = exception.Message }); }
    }
}
