using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Identity.Application.Licensing.AssignLicense;
using QualifyAI.Identity.Application.Licensing.GetEntitlements;
using QualifyAI.Identity.Application.Licensing.SetLicenseStatus;
using QualifyAI.Identity.Application.Licensing.UpdateLicense;
using QualifyAI.Identity.Domain.Licensing;

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

    [HttpPut("tenant/{tenantId:guid}")]
    public async Task<IActionResult> Update(
        Guid tenantId,
        [FromBody] UpdateLicenseRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(
            new UpdateLicenseCommand(
                tenantId,
                request.Plan,
                request.MaxUsers,
                request.ExpiresAtUtc,
                request.Modules),
            cancellationToken);
        return NoContent();
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

    [HttpPost("tenant/{tenantId:guid}/activate")]
    public Task<IActionResult> Activate(Guid tenantId, CancellationToken cancellationToken)
        => SetStatus(tenantId, LicenseStatus.Active, cancellationToken);

    [HttpPost("tenant/{tenantId:guid}/suspend")]
    public Task<IActionResult> Suspend(Guid tenantId, CancellationToken cancellationToken)
        => SetStatus(tenantId, LicenseStatus.Suspended, cancellationToken);

    [HttpPost("tenant/{tenantId:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid tenantId, CancellationToken cancellationToken)
        => SetStatus(tenantId, LicenseStatus.Cancelled, cancellationToken);

    private async Task<IActionResult> SetStatus(
        Guid tenantId,
        LicenseStatus status,
        CancellationToken cancellationToken)
    {
        await sender.Send(new SetLicenseStatusCommand(tenantId, status), cancellationToken);
        return NoContent();
    }
}

public sealed record AssignLicenseRequest(
    string Plan,
    DateTime StartsAtUtc,
    DateTime? ExpiresAtUtc,
    int MaxUsers,
    IReadOnlyCollection<string> Modules);

public sealed record UpdateLicenseRequest(
    string Plan,
    int MaxUsers,
    DateTime? ExpiresAtUtc,
    IReadOnlyCollection<string> Modules);
