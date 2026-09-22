using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LeadsAI.Infrastructure.WorkspacePackages;
using LeadsAI.Persistence.SqlServer;
using LeadsAI.Persistence.SqlServer.Projections;

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

        for (var attempt = 1; attempt <= 30; attempt++)
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

            // Development is intentionally self-healing on a clean database. Identity
            // publishes the authoritative entitlement events asynchronously, but a fresh
            // local environment must not block for two minutes when RabbitMQ delivery is
            // delayed or a previous broker queue is stale. The next identity event will
            // reconcile this projection with the authoritative license state.
            if (attempt == 10)
            {
                await EnsureDevelopmentEntitlementAsync(tenantId, cancellationToken);
                continue;
            }

            if (attempt < 30)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        logger.LogWarning(
            "Development seed skipped because tenant {TenantId} did not reach an active entitlement state.",
            tenantId);
    }

    private async Task EnsureDevelopmentEntitlementAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await db.TenantEntitlements
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);

        if (existing is null)
        {
            var now = DateTime.UtcNow;
            var plan = configuration["IdentityBootstrap:License:Plan"]?.Trim().ToLowerInvariant() ?? "enterprise";
            var maxUsers = configuration.GetValue("IdentityBootstrap:License:MaxUsers", 100);
            var modules = configuration
                .GetSection("IdentityBootstrap:License:Modules")
                .Get<string[]>()
                ?? [];

            existing = new TenantEntitlementProjection
            {
                TenantId = tenantId,
                TenantSlug = configuration["IdentityBootstrap:Tenant:Slug"]?.Trim().ToLowerInvariant() ?? "findleadsai",
                TenantStatus = "active",
                LicensePlan = plan,
                LicenseStatus = "active",
                MaxUsers = Math.Max(0, maxUsers),
                StartsAtUtc = now.AddMinutes(-5),
                ExpiresAtUtc = now.AddYears(1),
                Version = 1,
                ModulesJson = System.Text.Json.JsonSerializer.Serialize(modules),
                LimitsJson = System.Text.Json.JsonSerializer.Serialize(
                    new Dictionary<string, int> { ["users"] = Math.Max(0, maxUsers) }),
                UpdatedAtUtc = now
            };

            db.TenantEntitlements.Add(existing);
            await db.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Development entitlement projection was bootstrapped locally for tenant {TenantId}; Identity events will reconcile it.",
                tenantId);
        }
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
