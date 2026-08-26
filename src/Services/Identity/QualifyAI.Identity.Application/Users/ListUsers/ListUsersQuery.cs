using MediatR;
using QualifyAI.Identity.Application.Authentication;

namespace QualifyAI.Identity.Application.Users.ListUsers;

public sealed record ListUsersQuery(Guid TenantId) : IRequest<IReadOnlyList<AccountResult>>;

public sealed class ListUsersQueryHandler(IAccountService accounts)
    : IRequestHandler<ListUsersQuery, IReadOnlyList<AccountResult>>
{
    public Task<IReadOnlyList<AccountResult>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken)
        => accounts.ListUsersAsync(request.TenantId, cancellationToken);
}
