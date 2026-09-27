using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Identity.Application.Authentication.PasswordRecovery;

namespace QualifyAI.Identity.Api.Controllers.Authentication;

[ApiController]
[AllowAnonymous]
[Route("account")]
public sealed class PasswordRecoveryController(
    ISender sender,
    IHostEnvironment environment) : ControllerBase
{
    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword(
        [FromBody] ForgotPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var result = await sender.Send(
            new RequestPasswordResetCommand(request.Tenant, request.Email),
            cancellationToken);

        if (environment.IsDevelopment() && !string.IsNullOrWhiteSpace(result.ResetToken))
            return Ok(new { resetToken = result.ResetToken });

        return Accepted();
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromBody] ResetPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var reset = await sender.Send(
            new ResetPasswordCommand(
                request.Tenant,
                request.Email,
                request.Token,
                request.NewPassword),
            cancellationToken);

        return reset
            ? NoContent()
            : BadRequest(new { error = "invalid_or_expired_token" });
    }
}

public sealed record ForgotPasswordRequest(string Tenant, string Email);
public sealed record ResetPasswordRequest(
    string Tenant,
    string Email,
    string Token,
    string NewPassword);
