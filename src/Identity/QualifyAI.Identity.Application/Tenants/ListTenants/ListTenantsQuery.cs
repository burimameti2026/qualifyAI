using MediatR;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Application.Tenants.GetTenant;

namespace QualifyAI.Identity.Application.Tenants.ListTenants;

public sealed record ListTenantsQuery : IRequest<IReadOnlyList<TenantDetails>>;

public sealed class ListTenantsQueryHandler(ITenantRepository tenants)
    : IRequestHandler<ListTenantsQuery, IReadOnlyList<TenantDetails>>
{
    public async Task<IReadOnlyList<TenantDetails>> Handle(ListTenantsQuery request, CancellationToken cancellationToken)
        => (await tenants.ListAsync(cancellationToken)).Select(x => new TenantDetails(
            x.Id, x.Name, x.Slug, x.ContactEmail, x.Status.ToString(), x.CreatedAtUtc, x.UpdatedAtUtc)).ToArray();
}
