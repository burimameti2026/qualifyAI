using MediatR;
using QualifyAI.Identity.Application.Authentication;

namespace QualifyAI.Identity.Application.Users.SetRoles;

public sealed record SetUserRolesCommand(
    Guid TenantId,
    Guid UserId,
    IReadOnlyCollection<string> Roles) : IRequest;

public sealed class SetUserRolesCommandHandler(IAccountService accounts)
    : IRequestHandler<SetUserRolesCommand>
{
    public async Task Handle(
        SetUserRolesCommand request,
        CancellationToken cancellationToken)
    {
        await accounts.SetRolesAsync(
            request.TenantId,
            request.UserId,
            request.Roles,
            cancellationToken);
    }
}
