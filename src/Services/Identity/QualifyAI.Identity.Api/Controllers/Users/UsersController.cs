using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.BuildingBlocks.Security.Claims;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Application.Users.ChangePassword;
using QualifyAI.Identity.Application.Users.CreateUser;
using QualifyAI.Identity.Application.Users.GetUser;
using QualifyAI.Identity.Application.Users.ListUsers;
using QualifyAI.Identity.Application.Users.Mfa;
using QualifyAI.Identity.Application.Users.Security;
using QualifyAI.Identity.Application.Users.SetPermissions;
using QualifyAI.Identity.Application.Users.SetRoles;
using QualifyAI.Identity.Application.Users.SetStatus;

namespace QualifyAI.Identity.Api.Controllers.Users;

[ApiController]
[Authorize]
[Route("users")]
public sealed class UsersController(ISender sender) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AccountResult>>> List(CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId)) return Unauthorized();
        return Ok(await sender.Send(new ListUsersQuery(tenantId), cancellationToken));
    }

    [HttpGet("me")]
    public async Task<ActionResult<AccountResult>> Me(CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId) || !TryUserId(out var userId)) return Unauthorized();
        var result = await sender.Send(new GetUserQuery(tenantId, userId), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AccountResult>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId)) return Unauthorized();
        var result = await sender.Send(new GetUserQuery(tenantId, id), cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<AccountResult>> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId)) return Unauthorized();
        var tenantSlug = User.FindFirst(QualifyAiClaimTypes.TenantSlug)?.Value ?? string.Empty;
        var result = await sender.Send(
            new CreateUserCommand(tenantId, tenantSlug, request.Email, request.Password, request.FirstName, request.LastName, request.Roles ?? []),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> SetRoles(Guid id, [FromBody] RolesRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId)) return Unauthorized();
        await sender.Send(new SetUserRolesCommand(tenantId, id, request.Roles), cancellationToken);
        return NoContent();
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<IActionResult> SetPermissions(Guid id, [FromBody] PermissionsRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId)) return Unauthorized();
        await sender.Send(new SetUserPermissionsCommand(tenantId, id, request.Permissions), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:guid}/disable")]
    public Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken) => SetStatus(id, false, cancellationToken);

    [HttpPost("{id:guid}/enable")]
    public Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken) => SetStatus(id, true, cancellationToken);

    [HttpPost("me/change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId) || !TryUserId(out var userId)) return Unauthorized();
        await sender.Send(new ChangePasswordCommand(tenantId, userId, request.CurrentPassword, request.NewPassword), cancellationToken);
        await sender.Send(new RevokeSessionsCommand(tenantId, userId), cancellationToken);
        return NoContent();
    }

    [HttpPost("me/mfa/setup")]
    public async Task<ActionResult<MfaSetupResult>> BeginMfa(CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId) || !TryUserId(out var userId)) return Unauthorized();
        return Ok(await sender.Send(new BeginMfaCommand(tenantId, userId), cancellationToken));
    }

    [HttpPost("me/mfa/confirm")]
    public async Task<IActionResult> ConfirmMfa([FromBody] MfaCodeRequest request, CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId) || !TryUserId(out var userId)) return Unauthorized();
        var valid = await sender.Send(new ConfirmMfaCommand(tenantId, userId, request.Code), cancellationToken);
        if (!valid) return BadRequest(new { error = "invalid_code" });
        await sender.Send(new RevokeSessionsCommand(tenantId, userId), cancellationToken);
        return NoContent();
    }

    [HttpPost("me/mfa/recovery-codes")]
    public async Task<ActionResult<IReadOnlyCollection<string>>> GenerateRecoveryCodes(CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId) || !TryUserId(out var userId)) return Unauthorized();
        var codes = await sender.Send(new GenerateRecoveryCodesCommand(tenantId, userId), cancellationToken);
        return Ok(codes);
    }

    [HttpDelete("me/mfa")]
    public async Task<IActionResult> DisableMfa(CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId) || !TryUserId(out var userId)) return Unauthorized();
        await sender.Send(new DisableMfaCommand(tenantId, userId), cancellationToken);
        await sender.Send(new RevokeSessionsCommand(tenantId, userId), cancellationToken);
        return NoContent();
    }

    [HttpPost("me/revoke-sessions")]
    public async Task<IActionResult> RevokeSessions(CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId) || !TryUserId(out var userId)) return Unauthorized();
        await sender.Send(new RevokeSessionsCommand(tenantId, userId), cancellationToken);
        return NoContent();
    }

    private async Task<IActionResult> SetStatus(Guid userId, bool isActive, CancellationToken cancellationToken)
    {
        if (!TryTenantId(out var tenantId)) return Unauthorized();
        await sender.Send(new SetUserStatusCommand(tenantId, userId, isActive), cancellationToken);
        return NoContent();
    }

    private bool TryTenantId(out Guid tenantId)
        => Guid.TryParse(User.FindFirst(QualifyAiClaimTypes.TenantId)?.Value, out tenantId);

    private bool TryUserId(out Guid userId)
        => Guid.TryParse(User.FindFirstValue("sub"), out userId);
}

public sealed record CreateUserRequest(string Email, string Password, string FirstName, string LastName, string[]? Roles);
public sealed record RolesRequest(string[] Roles);
public sealed record PermissionsRequest(string[] Permissions);
public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public sealed record MfaCodeRequest(string Code);
