using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Identity.Application.Licensing.AssignLicense;
using QualifyAI.Identity.Application.Licensing.GetEntitlements;

namespace QualifyAI.Identity.Api.Controllers.Licensing;

[ApiController]
[Authorize]
[Route("api/identity/licenses")]
public sealed class LicensesController(ISender sender) : ControllerBase
{
    [HttpPost("tenant/{tenantId:guid}")]
    [ProducesResponseType<LicenseResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<LicenseResult>> Assign(
        Guid tenantId,
        [FromBody] AssignLicenseRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new AssignLicenseCommand(
                tenantId,
                request.Plan,
                request.StartsAtUtc,
                request.ExpiresAtUtc,
                request.MaxUsers,
                request.Modules),
            cancellationToken);

        return CreatedAtAction(nameof(GetEntitlements), new { tenantId }, result);
    }

    [HttpGet("tenant/{tenantId:guid}/entitlements")]
    [ProducesResponseType<TenantEntitlements>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantEntitlements>> GetEntitlements(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var entitlements = await sender.Send(
            new GetTenantEntitlementsQuery(tenantId),
            cancellationToken);

        return entitlements is null ? NotFound() : Ok(entitlements);
    }
}

public sealed record AssignLicenseRequest(
    string Plan,
    DateTime StartsAtUtc,
    DateTime? ExpiresAtUtc,
    int MaxUsers,
    IReadOnlyCollection<string> Modules);
