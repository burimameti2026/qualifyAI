using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using LeadsAI.Infrastructure.WorkspacePackages;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Demo;

public sealed class DevelopmentSeedService(
    AppDbContext db,
    LeadsAI.Infrastructure.WorkspacePackages.RealWorkspaceService workspace,
    LeadsAI.Infrastructure.WorkspacePackages.WorkspacePackageInstaller packageInstaller,
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
                await EnsureFusionFleetPackageAsync(tenantId, cancellationToken);
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

    private async Task EnsureFusionFleetPackageAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var packageId = configuration["DevelopmentSeed:WorkspacePackage"]?.Trim();
        if (string.IsNullOrWhiteSpace(packageId))
            return;

        const string markerKey = "acquisition.workspace-package.seed.v1";
        var marker = await db.TenantSettings.AsNoTracking()
            .AnyAsync(x => x.TenantId == tenantId && x.Key == markerKey && x.Value == packageId, cancellationToken);

        if (marker)
            return;

        await packageInstaller.InstallAsync(tenantId, packageId, cancellationToken);
        await SeedFusionFleetPackageLocalizationsAsync(tenantId, cancellationToken);

        db.TenantSettings.Add(new TenantSetting
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = markerKey,
            Value = packageId
        });
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded workspace package {PackageId} for tenant {TenantId}.", packageId, tenantId);
    }

    private async Task SeedFusionFleetPackageLocalizationsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string key = "acquisition.workspace-packages";
        var packages = new[]
        {
            new SavedWorkspacePackageSeed("en", "FusionFleet Logistics Growth", "AI-powered customer acquisition for logistics and transport companies", "Find high-fit shippers, fleet operators and 3PL prospects, qualify them with AI and automate the follow-up.", "Logistics companies, freight operators, 3PLs and fleet businesses", "299", new[] { "AI prospect discovery", "ICP qualification", "Automated outreach", "CRM pipeline", "Logistics growth workflows", "Revenue analytics" }),
            new SavedWorkspacePackageSeed("mk", "FusionFleet Логистички раст", "AI-платформа за пронаоѓање и освојување клиенти во логистиката", "Пронајдете компании со висок потенцијал, квалификувајте ги со AI и автоматизирајте го следењето до продажната можност.", "Логистички компании, транспортни оператори, 3PL компании и флота оператори", "299", new[] { "AI пронаоѓање потенцијални клиенти", "ICP квалификација", "Автоматизиран outreach", "CRM pipeline", "Логистички sales workflows", "Аналитика на приход" }),
            new SavedWorkspacePackageSeed("sq", "FusionFleet Rritje për Logjistikë", "Platformë me AI për gjetjen dhe fitimin e klientëve në logjistikë", "Gjeni kompani me potencial të lartë, kualifikojini me AI dhe automatizoni ndjekjen deri te mundësia e shitjes.", "Kompani logjistike, operatorë transporti, kompani 3PL dhe operatorë flotash", "299", new[] { "Zbulim i prospekteve me AI", "Kualifikim ICP", "Kontaktim i automatizuar", "Pipeline CRM", "Workflow për rritje në logjistikë", "Analitikë e të ardhurave" })
        };

        var setting = await db.TenantSettings.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Key == key, cancellationToken);
        var saved = packages.Select(x => new\n        {\n            Id = Guid.NewGuid(),\n            Name = x.Name,\n            Headline = x.Headline,\n            Subheadline = x.Subheadline,\n            Audience = x.Audience,\n            Price = x.Price,\n            Billing = "month",\n            Features = x.Features,\n            Sections = new[] { "Hero", "Problem", "AI acquisition", "Automation", "How it works", "Pricing", "Call to action" },\n            HiddenSections = Array.Empty<string>(),\n            UpdatedAtUtc = DateTime.UtcNow,\n            Language = x.Language\n        }).ToArray();
        var json = System.Text.Json.JsonSerializer.Serialize(saved);

        if (setting is null)
            db.TenantSettings.Add(new TenantSetting { Id = Guid.NewGuid(), TenantId = tenantId, Key = key, Value = json });
        else
        {
            setting.Value = json;
            setting.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record SavedWorkspacePackageSeed(string Language, string Name, string Headline, string Subheadline, string Audience, string Price, IReadOnlyList<string> Features);

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
