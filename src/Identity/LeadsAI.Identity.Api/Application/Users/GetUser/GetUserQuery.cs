using MediatR;
using LeadsAI.Identity.Application.Authentication;

namespace LeadsAI.Identity.Application.Users.GetUser;

public sealed record GetUserQuery(Guid TenantId, Guid UserId) : IRequest<AccountResult?>;

public sealed class GetUserQueryHandler(IAccountService accounts)
    : IRequestHandler<GetUserQuery, AccountResult?>
{
    public Task<AccountResult?> Handle(
        GetUserQuery request,
        CancellationToken cancellationToken)
        => accounts.GetUserAsync(request.TenantId, request.UserId, cancellationToken);
}
