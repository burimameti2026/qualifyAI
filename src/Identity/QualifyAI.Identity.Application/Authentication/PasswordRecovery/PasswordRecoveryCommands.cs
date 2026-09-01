using FluentValidation;
using MediatR;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Application;

namespace QualifyAI.Identity.Application.Authentication.PasswordRecovery;

public sealed record RequestPasswordResetCommand(string TenantSlug, string Email) : IRequest<PasswordResetRequestResult>;
public sealed record ResetPasswordCommand(string TenantSlug, string Email, string Token, string NewPassword) : IRequest<bool>;
public sealed record PasswordResetRequestResult(bool Accepted, string? ResetToken);

public sealed class RequestPasswordResetCommandValidator : AbstractValidator<RequestPasswordResetCommand>
{
    public RequestPasswordResetCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Token).NotEmpty();
        RuleFor(x => x.NewPassword).StrongIdentityPassword();
    }
}

public sealed class RequestPasswordResetCommandHandler(
    ITenantRepository tenants,
    IAccountService accounts)
    : IRequestHandler<RequestPasswordResetCommand, PasswordResetRequestResult>
{
    public async Task<PasswordResetRequestResult> Handle(
        RequestPasswordResetCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.GetBySlugAsync(request.TenantSlug, cancellationToken);
        if (tenant is null) return new PasswordResetRequestResult(true, null);

        try
        {
            var token = await accounts.GeneratePasswordResetTokenAsync(
                tenant.Id,
                request.Email,
                cancellationToken);
            return new PasswordResetRequestResult(true, token);
        }
        catch (KeyNotFoundException)
        {
            return new PasswordResetRequestResult(true, null);
        }
    }
}

public sealed class ResetPasswordCommandHandler(
    ITenantRepository tenants,
    IAccountService accounts)
    : IRequestHandler<ResetPasswordCommand, bool>
{
    public async Task<bool> Handle(
        ResetPasswordCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.GetBySlugAsync(request.TenantSlug, cancellationToken);
        if (tenant is null) return false;

        try
        {
            await accounts.ResetPasswordAsync(
                tenant.Id,
                request.Email,
                request.Token,
                request.NewPassword,
                cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
