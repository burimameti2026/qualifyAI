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
