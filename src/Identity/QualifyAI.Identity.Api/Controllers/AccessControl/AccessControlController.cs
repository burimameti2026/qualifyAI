using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Claims;
using QualifyAI.Identity.Application.AccessControl;
using QualifyAI.Identity.Domain.AccessControl;

namespace QualifyAI.Identity.Api.Controllers.AccessControl;

[ApiController]
[Authorize]
[Route("api/access-control")]
public sealed class AccessControlController(ISender sender) : ControllerBase
{
    [HttpGet("permissions")]
    public async Task<IActionResult> Permissions(CancellationToken ct)
    {
        if (!CanManageTenant()) return Forbid();
        return Ok(await sender.Send(new ListPermissionCatalogQuery(), ct));
    }

    [HttpGet("roles")]
    public async Task<IActionResult> Roles([FromQuery] Guid? tenantId, [FromQuery] bool includePlatform, CancellationToken ct)
    {
        var resolvedTenant = ResolveTenant(tenantId, allowPlatform: includePlatform);
        if (resolvedTenant.Denied) return Forbid();
        return Ok(await sender.Send(new ListRolesQuery(resolvedTenant.TenantId, includePlatform && IsPlatformAdmin()), ct));
    }

    [HttpPost("roles")]
    public async Task<IActionResult> CreateRole(CreateRoleRequest request, CancellationToken ct)
    {
        if (request.Scope == AccessRoleScope.Platform)
        {
            if (!IsPlatformAdmin()) return Forbid();
            var role = await sender.Send(new CreateRoleCommand(null, request.Name, request.Description, request.Scope, request.IsSystem, CurrentUserId()), ct);
            return Created($"/api/access-control/roles/{role.Id}", role);
        }

        var tenant = ResolveTenant(request.TenantId, allowPlatform: false);
        if (tenant.Denied || !tenant.TenantId.HasValue) return Forbid();
        var created = await sender.Send(new CreateRoleCommand(tenant.TenantId, request.Name, request.Description, AccessRoleScope.Tenant, request.IsSystem && IsPlatformAdmin(), CurrentUserId()), ct);
        return Created($"/api/access-control/roles/{created.Id}", created);
    }

    [HttpPut("roles/{roleId:guid}/permissions")]
    public async Task<IActionResult> SetRolePermissions(Guid roleId, PermissionGrantRequest request, CancellationToken ct)
    {
        var roles = await sender.Send(new ListRolesQuery(CurrentTenantId(), IsPlatformAdmin()), ct);
        var role = roles.FirstOrDefault(x => x.Id == roleId);
        if (role is null) return NotFound();
        if (role.Scope == AccessRoleScope.Platform && !IsPlatformAdmin()) return Forbid();
        if (role.Scope == AccessRoleScope.Tenant && role.TenantId != CurrentTenantId() && !IsPlatformAdmin()) return Forbid();
        await sender.Send(new SetRolePermissionsCommand(roleId, request.Permissions, CurrentUserId()), ct);
        return NoContent();
    }

    [HttpGet("clients/{clientApplicationId:guid}/permissions")]
    public async Task<IActionResult> ClientPermissions(Guid clientApplicationId, CancellationToken ct)
    {
        if (!CanManageTenant()) return Forbid();
        return Ok(await sender.Send(new GetClientPermissionsQuery(clientApplicationId), ct));
    }

    [HttpPut("clients/{clientApplicationId:guid}/permissions")]
    public async Task<IActionResult> SetClientPermissions(Guid clientApplicationId, PermissionGrantRequest request, CancellationToken ct)
    {
        if (!CanManageTenant()) return Forbid();
        await sender.Send(new SetClientPermissionsCommand(clientApplicationId, request.Permissions, CurrentUserId()), ct);
        return NoContent();
    }

    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] Guid? tenantId, [FromQuery] int take = 250, CancellationToken ct = default)
    {
        var tenant = ResolveTenant(tenantId, allowPlatform: true);
        if (tenant.Denied) return Forbid();
        return Ok(await sender.Send(new ListSecurityAuditQuery(tenant.TenantId, take), ct));
    }

    private bool CanManageTenant()
        => IsPlatformAdmin() || HasPermission(QualifyAiPermissions.UsersManage);

    private bool IsPlatformAdmin() => HasPermission(QualifyAiPermissions.SystemAdmin);

    private bool HasPermission(string permission)
        => User.FindAll(QualifyAiClaimTypes.Permission).Any(x => string.Equals(x.Value, permission, StringComparison.OrdinalIgnoreCase));

    private Guid? CurrentTenantId()
        => Guid.TryParse(User.FindFirst(QualifyAiClaimTypes.TenantId)?.Value, out var id) ? id : null;

    private Guid? CurrentUserId()
        => Guid.TryParse(User.FindFirstValue("sub"), out var id) ? id : null;

    private (Guid? TenantId, bool Denied) ResolveTenant(Guid? requested, bool allowPlatform)
    {
        if (IsPlatformAdmin()) return (requested, false);
        if (!CanManageTenant()) return (null, true);
        var own = CurrentTenantId();
        if (!own.HasValue) return (null, true);
        if (requested.HasValue && requested.Value != own.Value) return (null, true);
        if (allowPlatform && !requested.HasValue) return (own, false);
        return (own, false);
    }
}

public sealed record CreateRoleRequest(Guid? TenantId, string Name, string Description, AccessRoleScope Scope, bool IsSystem = false);
public sealed record PermissionGrantRequest(string[] Permissions);
