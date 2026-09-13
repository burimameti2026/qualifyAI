using LeadsAI.Identity.Application.AccessControl;

namespace LeadsAI.Identity.Infrastructure.Bootstrap;

public sealed class PermissionCatalogBootstrapHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAccessControlRepository>()
            .EnsurePermissionCatalogAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
