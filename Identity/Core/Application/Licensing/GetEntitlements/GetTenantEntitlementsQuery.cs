using MediatR;
using QualifyAI.Identity.Application.Abstractions.Persistence;

namespace QualifyAI.Identity.Application.Licensing.GetEntitlements;

public sealed record GetTenantEntitlementsQuery(Guid TenantId) : IRequest<TenantEntitlements?>;

//public sealed record TenantEntitlements(
//    Guid TenantId,
//    string Plan,
//    string LicenseStatus,
//    bool IsUsable,
//    int MaxUsers,
//    DateTime StartsAtUtc,
//    DateTime? ExpiresAtUtc,
//    long Version,
//    IReadOnlyCollection<string> Modules);

public sealed class GetTenantEntitlementsQueryHandler(ILicenseRepository licenses)
    : IRequestHandler<GetTenantEntitlementsQuery, TenantEntitlements?>
{
    public async Task<TenantEntitlements?> Handle(
        GetTenantEntitlementsQuery request,
        CancellationToken cancellationToken)
    {
        var license = await licenses.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (license is null) return null;

        return new TenantEntitlements(
            license.TenantId,
            license.Plan,
            license.Status.ToString(),
            license.IsUsable(DateTime.UtcNow),
            license.MaxUsers,
            license.StartsAtUtc,
            license.ExpiresAtUtc,
            license.Version,
            license.Modules.Where(x => x.Enabled).Select(x => x.Code).ToArray());
    }
}
