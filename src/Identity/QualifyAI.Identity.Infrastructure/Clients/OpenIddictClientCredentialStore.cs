using System.Collections.Immutable;
using OpenIddict.Abstractions;
using QualifyAI.Identity.Application.Clients;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QualifyAI.Identity.Infrastructure.Clients;

public sealed class OpenIddictClientCredentialStore(IOpenIddictApplicationManager applications)
    : IClientCredentialStore
{
    public async Task RegisterAsync(
        string clientId,
        string displayName,
        string clientSecret,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default)
    {
        if (await applications.FindByClientIdAsync(clientId, cancellationToken) is not null)
            throw new InvalidOperationException($"OpenIddict client '{clientId}' already exists.");

        var descriptor = BuildDescriptor(clientId, displayName, clientSecret, scopes);
        await applications.CreateAsync(descriptor, cancellationToken);
    }

    public async Task RotateSecretAsync(
        string clientId,
        string newClientSecret,
        CancellationToken cancellationToken = default)
    {
        var application = await applications.FindByClientIdAsync(clientId, cancellationToken)
            ?? throw new KeyNotFoundException("Client application not found in OpenIddict.");

        var descriptor = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(descriptor, application, cancellationToken);
        descriptor.ClientSecret = newClientSecret;
        await applications.UpdateAsync(application, descriptor, cancellationToken);
    }

    public Task SetEnabledAsync(
        string clientId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        // Enable/disable is enforced by the Identity domain metadata during token issuance.
        // OpenIddict remains the credential store and never stores plaintext secrets.
        return Task.CompletedTask;
    }

    private static OpenIddictApplicationDescriptor BuildDescriptor(
        string clientId,
        string displayName,
        string clientSecret,
        IReadOnlyCollection<string> scopes)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            DisplayName = displayName,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.ClientCredentials
            }
        };

        foreach (var scope in scopes
                     .Where(x => !string.IsNullOrWhiteSpace(x))
                     .Select(x => x.Trim().ToLowerInvariant())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            descriptor.Permissions.Add(Permissions.Prefixes.Scope + scope);
        }

        return descriptor;
    }
}
