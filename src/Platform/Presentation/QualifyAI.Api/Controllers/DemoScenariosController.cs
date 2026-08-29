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
    public Task<ScenarioInstallResult> Install(CancellationToken ct) => scenarios.InstallAsync(tenant.TenantId(), ct);

    [HttpPost("tenant/{tenantId:guid}/install")]
    [RequirePermission(QualifyAiPermissions.SystemAdmin)]
    public Task<ScenarioInstallResult> InstallForTenant(Guid tenantId, CancellationToken ct)
        => scenarios.InstallAsync(tenantId, ct);
}
