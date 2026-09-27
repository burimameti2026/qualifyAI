using MediatR;
using QualifyAI.Identity.Application.Authentication;

namespace QualifyAI.Identity.Application.Users.Security;

public sealed record GenerateRecoveryCodesCommand(Guid TenantId, Guid UserId) : IRequest<IReadOnlyCollection<string>>;
public sealed record RevokeSessionsCommand(Guid TenantId, Guid UserId) : IRequest;

public sealed class GenerateRecoveryCodesCommandHandler(ISecurityLifecycleService security)
    : IRequestHandler<GenerateRecoveryCodesCommand, IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(GenerateRecoveryCodesCommand request, CancellationToken cancellationToken)
        => security.GenerateMfaRecoveryCodesAsync(request.TenantId, request.UserId, cancellationToken);
}

public sealed class RevokeSessionsCommandHandler(ISecurityLifecycleService security)
    : IRequestHandler<RevokeSessionsCommand>
{
    public async Task Handle(RevokeSessionsCommand request, CancellationToken cancellationToken)
        => await security.RevokeSessionsAsync(request.TenantId, request.UserId, cancellationToken);
}
