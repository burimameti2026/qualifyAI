using MediatR;
using QualifyAI.Identity.Application.Abstractions.Persistence;

namespace QualifyAI.Identity.Application.Tenants.GetTenant;

public sealed record GetTenantQuery(Guid TenantId) : IRequest<TenantDetails?>;

public sealed record TenantDetails(
    Guid Id,
    string Name,
    string Slug,
    string ContactEmail,
    string Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed class GetTenantQueryHandler(ITenantRepository tenants)
    : IRequestHandler<GetTenantQuery, TenantDetails?>
{
    public async Task<TenantDetails?> Handle(
        GetTenantQuery request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.GetByIdAsync(request.TenantId, cancellationToken);
        return tenant is null
            ? null
            : new TenantDetails(
                tenant.Id,
                tenant.Name,
                tenant.Slug,
                tenant.ContactEmail,
                tenant.Status.ToString(),
                tenant.CreatedAtUtc,
                tenant.UpdatedAtUtc);
    }
}
