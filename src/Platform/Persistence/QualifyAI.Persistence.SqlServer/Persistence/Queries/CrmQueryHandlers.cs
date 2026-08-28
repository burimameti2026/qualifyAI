using MediatR;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Application.Queries.Crm;
using QualifyAI.Domain;

namespace QualifyAI.Persistence.SqlServer.Queries;

public sealed class ListContactsQueryHandler(ICrmRepository crm)
    : IRequestHandler<ListContactsQuery, IReadOnlyList<Contact>>
{
    public Task<IReadOnlyList<Contact>> Handle(ListContactsQuery request, CancellationToken cancellationToken)
        => crm.ListContactsAsync(request.TenantId, request.Take, cancellationToken);
}

public sealed class ListCompaniesQueryHandler(ICrmRepository crm)
    : IRequestHandler<ListCompaniesQuery, IReadOnlyList<Company>>
{
    public Task<IReadOnlyList<Company>> Handle(ListCompaniesQuery request, CancellationToken cancellationToken)
        => crm.ListCompaniesAsync(request.TenantId, cancellationToken);
}

public sealed class ListLeadsQueryHandler(ICrmRepository crm)
    : IRequestHandler<ListLeadsQuery, IReadOnlyList<Lead>>
{
    public Task<IReadOnlyList<Lead>> Handle(ListLeadsQuery request, CancellationToken cancellationToken)
        => crm.ListLeadsAsync(request.TenantId, cancellationToken);
}

public sealed class ListOpportunitiesQueryHandler(ICrmRepository crm)
    : IRequestHandler<ListOpportunitiesQuery, IReadOnlyList<Opportunity>>
{
    public Task<IReadOnlyList<Opportunity>> Handle(ListOpportunitiesQuery request, CancellationToken cancellationToken)
        => crm.ListOpportunitiesAsync(request.TenantId, cancellationToken);
}
