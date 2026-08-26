using MediatR;
using QualifyAI.Domain;

namespace QualifyAI.Application.Queries.Crm;

public sealed record ListContactsQuery(Guid TenantId, int Take = 500)
    : IRequest<IReadOnlyList<Contact>>;

public sealed record ListCompaniesQuery(Guid TenantId)
    : IRequest<IReadOnlyList<Company>>;

public sealed record ListLeadsQuery(Guid TenantId)
    : IRequest<IReadOnlyList<Lead>>;

public sealed record ListOpportunitiesQuery(Guid TenantId)
    : IRequest<IReadOnlyList<Opportunity>>;
