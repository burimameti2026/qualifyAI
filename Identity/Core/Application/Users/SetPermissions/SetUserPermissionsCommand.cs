using MediatR;
using QualifyAI.Identity.Application.Authentication;

namespace QualifyAI.Identity.Application.Users.SetPermissions;

public sealed record SetUserPermissionsCommand(
    Guid TenantId,
    Guid UserId,
    IReadOnlyCollection<string> Permissions) : IRequest;

public sealed class SetUserPermissionsCommandHandler(IAccountService accounts)
    : IRequestHandler<SetUserPermissionsCommand>
{
    public async Task Handle(
        SetUserPermissionsCommand request,
        CancellationToken cancellationToken)
    {
        await accounts.SetPermissionsAsync(
            request.TenantId,
            request.UserId,
            request.Permissions,
            cancellationToken);
    }
}
