using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LeadsAI.Infrastructure.WorkspacePackages;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Demo;

public sealed class DevelopmentSeedService(
    AppDbContext db,
    LeadsAI.Infrastructure.WorkspacePackages.RealWorkspaceService workspace,
    IConfiguration configuration,
    ILogger<DevelopmentSeedService> logger)
{
    private const string WorkspaceSettingKey = "real-workspace.v1";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
            return;

        var tenantIdValue = configuration["DevelopmentSeed:TenantId"]
            ?? configuration["TenantBootstrap:FindLeadsAI:TenantId"];

        if (!Guid.TryParse(tenantIdValue, out var tenantId))
        {
            logger.LogWarning("Development seed is enabled but no valid development TenantId is configured.");
            return;
        }

        for (var attempt = 1; attempt <= 60; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entitlement = await db.TenantEntitlements
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

            if (entitlement is not null &&
                entitlement.TenantStatus.Equals("active", StringComparison.OrdinalIgnoreCase) &&
                entitlement.LicenseStatus.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                await EnsureWorkspaceAsync(tenantId, cancellationToken);
                return;
            }

            if (attempt < 60)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        logger.LogWarning(
            "Development seed skipped because tenant {TenantId} did not reach an active entitlement state in time.",
            tenantId);
    }

    private async Task EnsureWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await db.TenantSettings
            .AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Key == WorkspaceSettingKey, cancellationToken);

        if (existing)
        {
            logger.LogInformation("Development workspace already exists for tenant {TenantId}.", tenantId);
            return;
        }

        var draft = await workspace.PrepareAsync(
            tenantId,
            new PrepareRealWorkspaceRequest(
                "sales",
                "sales-acquisition",
                "FindLeadsAI Development Workspace"),
            cancellationToken);

        await workspace.SaveAsync(
            tenantId,
            new SaveRealWorkspaceRequest(
                draft.WorkspaceId,
                draft.Name,
                Array.Empty<RealWorkspaceProspect>(),
                Array.Empty<string>()),
            cancellationToken);

        logger.LogInformation(
            "Provisioned empty development workspace {WorkspaceId} for tenant {TenantId}.",
            draft.WorkspaceId,
            tenantId);
    }
}

public sealed class DevelopmentSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DevelopmentSeedHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
            return;

        // Run after the host (and MassTransit bus) has started. The identity events
        // that create TenantEntitlements are asynchronous, so running the seed from
        // Program.cs before app.Run() creates a startup race and can never observe
        // the entitlement projections on a clean database.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var seed = scope.ServiceProvider.GetRequiredService<DevelopmentSeedService>();
                await seed.SeedAsync(stoppingToken);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Development seed attempt failed; retrying in 10 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
