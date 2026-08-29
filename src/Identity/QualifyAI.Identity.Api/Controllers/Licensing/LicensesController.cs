using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Identity.Application.Licensing.AssignLicense;
using QualifyAI.Identity.Application.Licensing.GetEntitlements;
using QualifyAI.Identity.Application.Licensing.SetLicenseStatus;
using QualifyAI.Identity.Application.Licensing.UpdateLicense;
using QualifyAI.Identity.Application.Licensing;
using QualifyAI.Identity.Domain.Licensing;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Claims;

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
        if (!CanManage(tenantId)) return Forbid();
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
        if (!CanManage(tenantId)) return Forbid();
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
        if (!CanRead(tenantId)) return Forbid();
        var entitlements = await sender.Send(
            new GetTenantEntitlementsQuery(tenantId),
            cancellationToken);

        return entitlements is null ? NotFound() : Ok(entitlements);
    }

    [HttpPost("tenant/{tenantId:guid}/activate")]
    public Task<IActionResult> Activate(Guid tenantId, CancellationToken cancellationToken)
        => CanManage(tenantId) ? SetStatus(tenantId, LicenseStatus.Active, cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpPost("tenant/{tenantId:guid}/suspend")]
    public Task<IActionResult> Suspend(Guid tenantId, CancellationToken cancellationToken)
        => CanManage(tenantId) ? SetStatus(tenantId, LicenseStatus.Suspended, cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpPost("tenant/{tenantId:guid}/cancel")]
    public Task<IActionResult> Cancel(Guid tenantId, CancellationToken cancellationToken)
        => CanManage(tenantId) ? SetStatus(tenantId, LicenseStatus.Cancelled, cancellationToken) : Task.FromResult<IActionResult>(Forbid());

    [HttpGet("catalog")]
    public IActionResult Catalog() => Ok(new
    {
        modules = QualifyAiModules.Enterprise,
        plans = LicensePlanCatalog.Plans
    });

    private async Task<IActionResult> SetStatus(
        Guid tenantId,
        LicenseStatus status,
        CancellationToken cancellationToken)
    {
        await sender.Send(new SetLicenseStatusCommand(tenantId, status), cancellationToken);
        return NoContent();
    }

    private bool CanRead(Guid tenantId) => IsOwnTenant(tenantId) &&
        (HasPermission(QualifyAiPermissions.BillingRead) || HasPermission(QualifyAiPermissions.BillingManage) || HasPermission(QualifyAiPermissions.SystemAdmin));

    private bool CanManage(Guid tenantId) => IsOwnTenant(tenantId) &&
        (HasPermission(QualifyAiPermissions.BillingManage) || HasPermission(QualifyAiPermissions.SystemAdmin));

    private bool IsOwnTenant(Guid tenantId) => Guid.TryParse(User.FindFirst(QualifyAiClaimTypes.TenantId)?.Value, out var current) && current == tenantId;
    private bool HasPermission(string permission) => User.FindAll(QualifyAiClaimTypes.Permission).Any(x => x.Value.Equals(permission, StringComparison.OrdinalIgnoreCase));
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
