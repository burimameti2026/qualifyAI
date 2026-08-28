using MediatR;
using QualifyAI.Identity.Application.Authentication;

namespace QualifyAI.Identity.Application.Users.SetStatus;

public sealed record SetUserStatusCommand(
    Guid TenantId,
    Guid UserId,
    bool IsActive) : IRequest;

public sealed class SetUserStatusCommandHandler(IAccountService accounts)
    : IRequestHandler<SetUserStatusCommand>
{
    public async Task Handle(
        SetUserStatusCommand request,
        CancellationToken cancellationToken)
    {
        if (request.IsActive)
            await accounts.EnableAsync(request.TenantId, request.UserId, cancellationToken);
        else
            await accounts.DisableAsync(request.TenantId, request.UserId, cancellationToken);
    }
}
