using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LeadsAI.Infrastructure.IndustryPacks;
using LeadsAI.Persistence.SqlServer;
using LeadsAI.Domain;

namespace LeadsAI.Infrastructure.Demo;

public sealed class DevelopmentSeedService(
    AppDbContext db,
    IIndustryPackProvisioner industryPackProvisioner,
    IConfiguration configuration,
    ILogger<DevelopmentSeedService> logger)
{
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
                await EnsureIndustryPackCampaignAsync(tenantId, cancellationToken);
                return;
            }

            if (attempt < 30)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        logger.LogError(
            "Development seed could not continue because tenant {TenantId} did not receive an active entitlement projection. " +
            "Identity bootstrap/outbox or Platform RabbitMQ consumers must be investigated.",
            tenantId);
    }

    private async Task EnsureIndustryPackCampaignAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var code = configuration["DevelopmentSeed:IndustryPackCode"]?.Trim();

        if (string.IsNullOrWhiteSpace(code))
        {
            logger.LogInformation(
                "Development seed has no IndustryPackCode configured; skipping acquisition provisioning.");
            return;
        }

        var pack = await db.IndustryPacks
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.Code == code, cancellationToken);

        if (pack is null)
        {
            logger.LogWarning(
                "Development seed requested IndustryPackCode '{IndustryPackCode}', but no IndustryPack exists. " +
                "Seed the IndustryPack first; no workspace package will be created.",
                code);
            return;
        }

        var result = await industryPackProvisioner.ProvisionAsync(
            tenantId,
            pack.Id,
            cancellationToken);

        logger.LogInformation(
            "Provisioned industry pack {IndustryPackCode} for tenant {TenantId}. Campaign {CampaignId}, TargetList {TargetListId}.",
            pack.Code,
            tenantId,
            result.CampaignId,
            result.TargetListId);
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
