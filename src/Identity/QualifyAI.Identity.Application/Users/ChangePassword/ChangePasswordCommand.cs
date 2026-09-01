using FluentValidation;
using MediatR;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Application;

namespace QualifyAI.Identity.Application.Users.ChangePassword;

public sealed record ChangePasswordCommand(
    Guid TenantId,
    Guid UserId,
    string CurrentPassword,
    string NewPassword) : IRequest;

public sealed class ChangePasswordCommandValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.CurrentPassword).NotEmpty();
        RuleFor(x => x.NewPassword).StrongIdentityPassword();
    }
}

public sealed class ChangePasswordCommandHandler(IAccountService accounts)
    : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(
        ChangePasswordCommand request,
        CancellationToken cancellationToken)
    {
        await accounts.ChangePasswordAsync(
            request.TenantId,
            request.UserId,
            request.CurrentPassword,
            request.NewPassword,
            cancellationToken);
    }
}
