using MediatR;
using LeadsAI.Identity.Application.Authentication;

namespace LeadsAI.Identity.Application.Users.ListUsers;

public sealed record ListUsersQuery(Guid TenantId) : IRequest<IReadOnlyList<AccountResult>>;

public sealed class ListUsersQueryHandler(IAccountService accounts)
    : IRequestHandler<ListUsersQuery, IReadOnlyList<AccountResult>>
{
    public Task<IReadOnlyList<AccountResult>> Handle(
        ListUsersQuery request,
        CancellationToken cancellationToken)
        => accounts.ListUsersAsync(request.TenantId, cancellationToken);
}
