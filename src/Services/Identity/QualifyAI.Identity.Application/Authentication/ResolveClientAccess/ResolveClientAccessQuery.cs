using MediatR;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Domain.Clients;
using QualifyAI.Identity.Domain.Tenants;

namespace QualifyAI.Identity.Application.Authentication.ResolveClientAccess;

public sealed record ResolveClientAccessQuery(string ClientId) : IRequest<ClientAccessSnapshot?>;

public sealed record ClientAccessSnapshot(
    string ClientId,
    string DisplayName,
    Guid? TenantId,
    string? TenantSlug,
    bool TenantActive,
    bool LicenseUsable,
    string? LicensePlan,
    string? LicenseStatus,
    long? LicenseVersion,
    IReadOnlyCollection<string> Modules,
    IReadOnlyCollection<string> AllowedScopes);

public sealed class ResolveClientAccessQueryHandler(
    IClientApplicationRepository clients,
    ITenantRepository tenants,
    ILicenseRepository licenses)
    : IRequestHandler<ResolveClientAccessQuery, ClientAccessSnapshot?>
{
    public async Task<ClientAccessSnapshot?> Handle(
        ResolveClientAccessQuery request,
        CancellationToken cancellationToken)
    {
        var client = await clients.GetByClientIdAsync(request.ClientId.Trim().ToLowerInvariant(), cancellationToken);
        if (client is null || client.Status != ClientApplicationStatus.Active)
            return null;

        var allowedScopes = client.Scopes.Select(x => x.Name).ToArray();

        // Platform/service clients may intentionally be tenant-neutral.
        if (!client.TenantId.HasValue)
        {
            return new ClientAccessSnapshot(
                client.ClientId,
                client.DisplayName,
                null,
                null,
                true,
                true,
                null,
                null,
                null,
                [],
                allowedScopes);
        }

        var tenant = await tenants.GetByIdAsync(client.TenantId.Value, cancellationToken);
        if (tenant is null)
            return null;

        var license = await licenses.GetByTenantIdAsync(tenant.Id, cancellationToken);
        var licenseUsable = license?.IsUsable(DateTime.UtcNow) == true;

        return new ClientAccessSnapshot(
            client.ClientId,
            client.DisplayName,
            tenant.Id,
            tenant.Slug,
            tenant.Status == TenantStatus.Active,
            licenseUsable,
            license?.Plan,
            license?.Status.ToString(),
            license?.Version,
            license?.Modules.Select(x => x.Code).ToArray() ?? [],
            allowedScopes);
    }
}
