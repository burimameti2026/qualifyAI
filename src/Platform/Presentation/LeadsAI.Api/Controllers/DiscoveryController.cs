using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Api.Services;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/acquisition")]
public sealed class DiscoveryController(ITenantContext tenant, ProspectDiscoveryService discovery) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("discovery/providers")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public IActionResult DiscoveryProviders() => Ok(discovery.ProviderStatus());

    [HttpPost("icp/{id:guid}

    [HttpPost("icp/{id:guid}/discover")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Discover(Guid id, [FromBody] DiscoveryRequest? input, CancellationToken ct)
    {
        try
        {
            var request = input ?? new DiscoveryRequest();
            var result = await discovery.DiscoverAsync(TenantId, id, new DiscoveryRunOptions(
                request.Source, request.Region, request.MaximumResults, request.MinimumScore,
                request.TargetListName, request.CreateTargetList), ct);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { code = "discovery_not_ready", detail = exception.Message });
        }
    }

    [HttpPost("discovery/providers/{name}/verify")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> VerifyDiscoveryProvider(string name, CancellationToken ct)
    {
        try
        {
            var result = await discovery.VerifyProviderAsync(name, ct);
            return result.Verified
                ? Ok(result)
                : BadRequest(result);
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new
            {
                code = "discovery_provider_not_found",
                detail = exception.Message
            });
        }
    }
}
