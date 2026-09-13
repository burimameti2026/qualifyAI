using MediatR;
using LeadsAI.Identity.Application.Abstractions.Persistence;
using LeadsAI.Identity.Domain.Licensing;
using LeadsAI.Identity.Domain.Tenants;

namespace LeadsAI.Identity.Application.Authentication.ResolveTenantAccess;

public sealed record ResolveTenantAccessQuery(string TenantSlug) : IRequest<TenantAccessSnapshot?>;

public sealed record TenantAccessSnapshot(
    Guid TenantId,
    string TenantSlug,
    bool TenantActive,
    string? LicensePlan,
    string? LicenseStatus,
    long LicenseVersion,
    bool LicenseUsable,
    IReadOnlyCollection<string> Modules);

public sealed class ResolveTenantAccessQueryHandler(
    ITenantRepository tenants,
    ILicenseRepository licenses)
    : IRequestHandler<ResolveTenantAccessQuery, TenantAccessSnapshot?>
{
    public async Task<TenantAccessSnapshot?> Handle(
     ResolveTenantAccessQuery request,
     CancellationToken cancellationToken)
    {
        var tenant = await tenants.GetBySlugAsync(request.TenantSlug, cancellationToken);
        if(tenant is null) return null;

        var license = await licenses.GetByTenantIdAsync(tenant.Id, cancellationToken);

        var status = license?.Status??LicenseStatus.Cancelled;

        bool tenantActive = status is LicenseStatus.Active or LicenseStatus.Trial or LicenseStatus.GracePeriod;

        return new TenantAccessSnapshot(
            tenant.Id,
            tenant.Slug,
            tenantActive,
            license?.Plan,
            status.ToString(),
            license?.Version??0,
            license?.IsUsable(DateTime.UtcNow)??false,
            license?.Modules.Where(x => x.Enabled).Select(x => x.Code).ToArray()?? []);
    }
}
