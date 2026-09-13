using MediatR;
using LeadsAI.BuildingBlocks.Application.Security;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.Domain;

namespace LeadsAI.Application.Queries.Crm;

[AccessControl(QualifyAiPermissions.CrmRead, QualifyAiModules.Crm)]
public sealed record ListContactsQuery(Guid TenantId, int Take = 500)
    : IRequest<IReadOnlyList<Contact>>;

[AccessControl(QualifyAiPermissions.CrmRead, QualifyAiModules.Crm)]
public sealed record ListCompaniesQuery(Guid TenantId)
    : IRequest<IReadOnlyList<Company>>;

[AccessControl(QualifyAiPermissions.CrmRead, QualifyAiModules.Crm)]
public sealed record ListLeadsQuery(Guid TenantId)
    : IRequest<IReadOnlyList<Lead>>;

[AccessControl(QualifyAiPermissions.CrmRead, QualifyAiModules.Crm)]
public sealed record ListOpportunitiesQuery(Guid TenantId)
    : IRequest<IReadOnlyList<Opportunity>>;
