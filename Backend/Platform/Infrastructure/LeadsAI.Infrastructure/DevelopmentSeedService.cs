using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using LeadsAI.Infrastructure.IndustryPacks;
using LeadsAI.Persistence.SqlServer;
using LeadsAI.Domain;

namespace LeadsAI.Infrastructure.Demo;

public sealed class DevelopmentSeedService(
    AppDbContext db,
    IIndustryPackProvisioner industryPackProvisioner,
    WorkspacePackages.WorkspacePackageInstaller workspacePackageInstaller,
    IConfiguration configuration,
    ILogger<DevelopmentSeedService> logger)
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue<bool>("DevelopmentSeed:Enabled"))
            return;

        var tenantSlug = configuration["DevelopmentSeed:TenantSlug"]?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(tenantSlug))
        {
            logger.LogWarning("Development seed is enabled but no TenantSlug is configured.");
            return;
        }

        for (var attempt = 1; attempt <= 30; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var entitlement = await db.TenantEntitlements
                .AsNoTracking()
                .FirstOrDefaultAsync(x =>
                    x.TenantSlug == tenantSlug &&
                    x.TenantStatus == "active" &&
                    x.LicenseStatus == "active",
                    cancellationToken);

            if (entitlement is not null)
            {
                logger.LogInformation(
                    "Development seed resolved tenant {TenantSlug} to {TenantId}.",
                    tenantSlug,
                    entitlement.TenantId);

                await workspacePackageInstaller.InstallAsync(entitlement.TenantId, "logistics", cancellationToken);
                await EnsureIndustryPackCampaignAsync(entitlement.TenantId, cancellationToken);
                return;
            }

            logger.LogInformation(
                "Development seed waiting for active entitlement for tenant {TenantSlug}. Attempt {Attempt}/30.",
                tenantSlug,
                attempt);

            if (attempt < 30)
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        logger.LogError(
            "Development seed could not continue because tenant {TenantSlug} did not receive an active entitlement projection. " +
            "Identity bootstrap/outbox or Platform RabbitMQ consumers must be investigated.",
            tenantSlug);
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
            .SingleOrDefaultAsync(x => x.Code == code, cancellationToken);

        if (pack is null)
        {
            pack = new IndustryPack
            {
                Id = Guid.NewGuid(),
                Code = code,
                Name = "FusionFleet Mk Logistics Sales",
                Description = "Campaign-ready logistics prospecting blueprint for FusionFleet Mk.",
                TemplateJson = JsonSerializer.Serialize(new
                {
                    version = 1,
                    industry = "Logistics & Transport",
                    region = "Balkans",
                    countries = new[] { "MK", "AL", "XK" },
                    languages = new[] { "en", "mk", "sq" },
                    purpose = "Find and qualify logistics companies for FusionFleet.",
                    offer = "Fleet and transport operations software.",
                    audience = "Logistics companies, transport operators, freight forwarders and 3PL providers.",
                    minimumScore = 70,
                    discovery = new { provider = "serpapi", keywords = new[] { "logistics companies", "transport companies", "freight forwarders", "3PL providers", "warehouse operators" } },
                    qualification = new { minimumScore = 70 },
                    enrichment = new { enabled = true },
                    targetList = new { enabled = true },
                    outreach = new { definition = "Personalized B2B outreach after qualification; human approval before delivery." },
                    approvalRequired = true,
                    scenarios = new[]
                    {
                        new { name = "Logistics Companies", code = "logistics-companies" },
                        new { name = "Transport Companies", code = "transport-companies" },
                        new { name = "Freight Forwarders", code = "freight-forwarders" },
                        new { name = "3PL Providers", code = "3pl-providers" },
                        new { name = "Warehouse Operators", code = "warehouse-operators" }
                    }
                })
            };
            db.IndustryPacks.Add(pack);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Development seed created IndustryPack {IndustryPackCode}.", code);
        }

        var result = await industryPackProvisioner.ProvisionAsync(
            tenantId,
            pack.Id,
            cancellationToken,
            scenarioCode: "logistics-companies");

        logger.LogInformation(
            "Provisioned industry pack {IndustryPackCode} for tenant {TenantId}. Campaign {CampaignId}, TargetList {TargetListId}.",
            pack.Code,
            tenantId,
            result.CampaignId,
            result.TargetListId);
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
