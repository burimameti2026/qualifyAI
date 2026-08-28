using MediatR;
using QualifyAI.Identity.Application.Authentication;

namespace QualifyAI.Identity.Application.Users.Mfa;

public sealed record BeginMfaCommand(Guid TenantId, Guid UserId) : IRequest<MfaSetupResult>;
public sealed record ConfirmMfaCommand(Guid TenantId, Guid UserId, string Code) : IRequest<bool>;
public sealed record DisableMfaCommand(Guid TenantId, Guid UserId) : IRequest;

public sealed class BeginMfaCommandHandler(IAccountService accounts)
    : IRequestHandler<BeginMfaCommand, MfaSetupResult>
{
    public Task<MfaSetupResult> Handle(BeginMfaCommand request, CancellationToken cancellationToken)
        => accounts.BeginMfaAsync(request.TenantId, request.UserId, cancellationToken);
}

public sealed class ConfirmMfaCommandHandler(IAccountService accounts)
    : IRequestHandler<ConfirmMfaCommand, bool>
{
    public Task<bool> Handle(ConfirmMfaCommand request, CancellationToken cancellationToken)
        => accounts.ConfirmMfaAsync(request.TenantId, request.UserId, request.Code, cancellationToken);
}

public sealed class DisableMfaCommandHandler(IAccountService accounts)
    : IRequestHandler<DisableMfaCommand>
{
    public async Task Handle(DisableMfaCommand request, CancellationToken cancellationToken)
    {
        await accounts.DisableMfaAsync(request.TenantId, request.UserId, cancellationToken);
    }
}
