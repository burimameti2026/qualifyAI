using FluentValidation;
using MediatR;
using QualifyAI.Identity.Application;
using QualifyAI.Identity.Application.Authentication;

namespace QualifyAI.Identity.Application.Users.CreateUser;

public sealed record CreateUserCommand(
    Guid TenantId,
    string TenantSlug,
    string Email,
    string Password,
    string FirstName,
    string LastName,
    IReadOnlyCollection<string> Roles) : IRequest<AccountResult>;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.TenantSlug).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Password).StrongIdentityPassword();
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
    }
}

public sealed class CreateUserCommandHandler(IAccountService accounts)
    : IRequestHandler<CreateUserCommand, AccountResult>
{
    public Task<AccountResult> Handle(
        CreateUserCommand request,
        CancellationToken cancellationToken)
        => accounts.CreateUserAsync(
            new CreateAccountRequest(
                request.TenantId,
                request.TenantSlug,
                request.Email,
                request.Password,
                request.FirstName,
                request.LastName,
                request.Roles),
            cancellationToken);
}
