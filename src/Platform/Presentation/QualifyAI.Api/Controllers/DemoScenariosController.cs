using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.Demo;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Automation)]
[Route("api/demo-scenarios")]
public sealed class DemoScenariosController(ITenantContext tenant, RealisticScenarioService scenarios) : ControllerBase
{
    [HttpPost("install")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Install(CancellationToken ct)
    {
        try { return Ok(await scenarios.InstallAsync(tenant.TenantId(), ct)); }
        catch (InvalidOperationException exception) { return Conflict(new { detail = exception.Message }); }
    }

    [HttpPost("reset")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public Task<ScenarioResetResult> Reset(CancellationToken ct) => scenarios.ResetBusinessDataAsync(tenant.TenantId(), ct);

    [HttpPost("reset-and-install")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public Task<ResetAndInstallResult> ResetAndInstall(CancellationToken ct)
        => scenarios.ResetAndInstallAsync(tenant.TenantId(), ct);

    [HttpPost("tenant/{tenantId:guid}/install")]
    [RequirePermission(QualifyAiPermissions.SystemAdmin)]
    public async Task<IActionResult> InstallForTenant(Guid tenantId, CancellationToken ct)
    {
        try { return Ok(await scenarios.InstallAsync(tenantId, ct)); }
        catch (InvalidOperationException exception) { return Conflict(new { detail = exception.Message }); }
    }

    [HttpPost("tenant/{tenantId:guid}/reset-and-install")]
    [RequirePermission(QualifyAiPermissions.SystemAdmin)]
    public Task<ResetAndInstallResult> ResetAndInstallForTenant(Guid tenantId, CancellationToken ct)
        => scenarios.ResetAndInstallAsync(tenantId, ct);
}
