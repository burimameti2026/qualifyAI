using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LeadsAI.Identity.Infrastructure.Bootstrap;

public sealed class OpenIddictCompatibilityBootstrapHostedService(
    IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await scopeManager.FindByNameAsync("qualifyai-api", cancellationToken) is null)
        {
            await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
            {
                Name = "qualifyai-api",
                DisplayName = "QualifyAI API",
                Resources = { "leadsai-api" }
            }, cancellationToken);
        }

        var client = new OpenIddictApplicationDescriptor
        {
            ClientId = "qualifyai-admin",
            DisplayName = "QualifyAI Admin UI",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            Permissions =
            {
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.Password,
                Permissions.GrantTypes.RefreshToken,
                Permissions.Prefixes.Scope + "openid",
                Permissions.Prefixes.Scope + "profile",
                Permissions.Prefixes.Scope + "email",
                Permissions.Prefixes.Scope + "offline_access",
                Permissions.Prefixes.Scope + "qualifyai-api",
                Permissions.Prefixes.Scope + "leadsai-api"
            }
        };

        var existing = await applicationManager.FindByClientIdAsync(client.ClientId, cancellationToken);
        if (existing is null)
            await applicationManager.CreateAsync(client, cancellationToken);
        else
            await applicationManager.UpdateAsync(existing, client, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
