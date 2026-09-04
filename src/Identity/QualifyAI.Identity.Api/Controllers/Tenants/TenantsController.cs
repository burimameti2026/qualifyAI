using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Identity.Application.Tenants.CreateTenant;
using QualifyAI.Identity.Application.Tenants.GetTenant;
using QualifyAI.Identity.Application.Tenants.ListTenants;
using QualifyAI.Identity.Application.Tenants.SetStatus;
using QualifyAI.Identity.Application.Tenants.ProvisionTenant;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Application.Users.CreateUser;
using QualifyAI.Identity.Domain.Tenants;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Claims;

namespace QualifyAI.Identity.Api.Controllers.Tenants;

[ApiController]
[Authorize]
[Route("api/identity/tenants")]
public sealed class TenantsController(ISender sender) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<CreateTenantResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CreateTenantResult>> Create([FromBody] CreateTenantRequest request, CancellationToken cancellationToken)
    {
        if (!IsSystemAdmin()) return Forbid();
        var result = await sender.Send(new CreateTenantCommand(request.Name, request.Slug, request.ContactEmail), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { tenantId = result.Id }, result);
    }

    [HttpPost("provision")]
    [ProducesResponseType<ProvisionTenantResult>(StatusCodes.Status201Created)]
    public async Task<ActionResult<ProvisionTenantResult>> Provision([FromBody] ProvisionTenantRequest request, CancellationToken cancellationToken)
    {
        if (!IsSystemAdmin()) return Forbid();
        var result = await sender.Send(new ProvisionTenantCommand(request.Name, request.Slug, request.ContactEmail, request.Plan, request.StartsAtUtc, request.ExpiresAtUtc, request.GracePeriodEndsAtUtc, request.MaxUsers, request.Modules, request.OwnerEmail, request.OwnerPassword, request.OwnerFirstName, request.OwnerLastName), cancellationToken);
        return CreatedAtAction(nameof(GetById), new { tenantId = result.TenantId }, result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantDetails>>> List(CancellationToken cancellationToken)
    {
        if (!IsSystemAdmin()) return Forbid();
        return Ok(await sender.Send(new ListTenantsQuery(), cancellationToken));
    }

    [HttpGet("{tenantId:guid}")]
    public async Task<ActionResult<TenantDetails>> GetById(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!CanRead(tenantId)) return Forbid();
        var tenant = await sender.Send(new GetTenantQuery(tenantId), cancellationToken);
        return tenant is null ? NotFound() : Ok(tenant);
    }

    [HttpPost("{tenantId:guid}/activate")]
    public async Task<IActionResult> Activate(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!IsSystemAdmin()) return Forbid();
        await sender.Send(new SetTenantStatusCommand(tenantId, TenantStatus.Active), cancellationToken);
        return NoContent();
    }

    [HttpPost("{tenantId:guid}/suspend")]
    public async Task<IActionResult> Suspend(Guid tenantId, CancellationToken cancellationToken)
    {
        if (!IsSystemAdmin()) return Forbid();
        await sender.Send(new SetTenantStatusCommand(tenantId, TenantStatus.Suspended), cancellationToken);
        return NoContent();
    }

    [HttpPost("{tenantId:guid}/admin")]
    public async Task<ActionResult<AccountResult>> CreateAdmin(Guid tenantId, [FromBody] CreateTenantAdminRequest request, CancellationToken cancellationToken)
    {
        if (!IsSystemAdmin()) return Forbid();
        var tenant = await sender.Send(new GetTenantQuery(tenantId), cancellationToken);
        if (tenant is null) return NotFound();
        var account = await sender.Send(new CreateUserCommand(tenantId, tenant.Slug, request.Email, request.Password, request.FirstName, request.LastName, ["Admin"]), cancellationToken);
        return Created($"/users/{account.Id}", account);
    }

    private bool CanRead(Guid tenantId) => IsSystemAdmin() || (Guid.TryParse(User.FindFirst(QualifyAiClaimTypes.TenantId)?.Value, out var current) && current == tenantId);
    private bool IsSystemAdmin() => User.FindAll(QualifyAiClaimTypes.Permission).Any(x => x.Value.Equals(QualifyAiPermissions.SystemAdmin, StringComparison.OrdinalIgnoreCase));
}

public sealed record CreateTenantRequest(string Name, string Slug, string ContactEmail);
public sealed record ProvisionTenantRequest(string Name, string Slug, string ContactEmail, string Plan, DateTime StartsAtUtc, DateTime? ExpiresAtUtc, DateTime? GracePeriodEndsAtUtc, int? MaxUsers, IReadOnlyCollection<string>? Modules, string OwnerEmail, string OwnerPassword, string OwnerFirstName, string OwnerLastName);
public sealed record CreateTenantAdminRequest(string Email, string Password, string FirstName, string LastName);
