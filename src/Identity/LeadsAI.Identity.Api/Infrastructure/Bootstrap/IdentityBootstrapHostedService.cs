using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using OpenIddict.Abstractions;
using LeadsAI.BuildingBlocks.Messaging.Outbox;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.Contracts.Identity;
using LeadsAI.Identity.Domain.Licensing;
using LeadsAI.Identity.Domain.Tenants;
using LeadsAI.Identity.Persistence.SqlServer.Identity;
using LeadsAI.Identity.Persistence.SqlServer;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LeadsAI.Identity.Infrastructure.Bootstrap;

public sealed class IdentityBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<IdentityBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("IdentityBootstrap:Enabled", true))
        {
            logger.LogInformation("Identity bootstrap is disabled.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var applicationManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        await MigrateDatabaseAsync(dbContext, cancellationToken);
        await EnsureAdminUiClientAsync(applicationManager, cancellationToken);

        var tenantSlug = configuration["IdentityBootstrap:Tenant:Slug"]?.Trim().ToLowerInvariant() ?? "demo";
        var tenantName = configuration["IdentityBootstrap:Tenant:Name"]?.Trim() ?? "LeadsAI Demo";
        var contactEmail = configuration["IdentityBootstrap:Tenant:ContactEmail"]?.Trim().ToLowerInvariant() ?? "admin@demo.local";
        var configuredTenantId = ParseOptionalTenantId(configuration["IdentityBootstrap:Tenant:Id"]);

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Slug == tenantSlug, cancellationToken);
        if (tenant is null)
        {
            tenant = configuredTenantId.HasValue
                ? Tenant.Create(configuredTenantId.Value, tenantName, tenantSlug, contactEmail)
                : Tenant.Create(tenantName, tenantSlug, contactEmail);
            await dbContext.Tenants.AddAsync(tenant, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Provisioned bootstrap tenant {TenantSlug} ({TenantId}).", tenant.Slug, tenant.Id);
        }
        else if (configuredTenantId.HasValue && tenant.Id != configuredTenantId.Value)
        {
            logger.LogWarning("Bootstrap tenant '{TenantSlug}' already exists with id '{ExistingTenantId}'. Configured id '{ConfiguredTenantId}' is used only when creating the tenant.", tenantSlug, tenant.Id, configuredTenantId.Value);
        }

        var license = await dbContext.Licenses
            .Include(x => x.Modules)
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id, cancellationToken);

        if (license is null)
        {
            var modules = configuration
                .GetSection("IdentityBootstrap:License:Modules")
                .Get<string[]>()
                ?? QualifyAiModules.Enterprise;

            license = License.Create(
                tenant.Id,
                configuration["IdentityBootstrap:License:Plan"] ?? "Enterprise",
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddYears(1),
                configuration.GetValue("IdentityBootstrap:License:MaxUsers", 100),
                modules);

            await dbContext.Licenses.AddAsync(license, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Provisioned bootstrap license {LicenseId} for tenant {TenantId}.", license.Id, tenant.Id);
        }

        var snapshotAtUtc = DateTime.UtcNow;
        outbox.Add(new TenantCreatedIntegrationEvent(
            Guid.NewGuid(), snapshotAtUtc, tenant.Id, tenant.Slug, tenant.Name, tenant.ContactEmail));
        outbox.Add(new TenantLicenseChangedIntegrationEvent(
            Guid.NewGuid(), snapshotAtUtc, tenant.Id, tenant.Slug, license.Id, license.Plan,
            license.Status.ToString().ToLowerInvariant(), license.MaxUsers, license.StartsAtUtc,
            license.ExpiresAtUtc, license.Version, license.Modules.Select(x => x.Code).ToArray()));
        await dbContext.SaveChangesAsync(cancellationToken);

        var adminEmail = configuration["IdentityBootstrap:Admin:Email"]?.Trim().ToLowerInvariant() ?? contactEmail;
        var adminPassword = configuration["IdentityBootstrap:Admin:Password"] ?? "Admin123!ChangeMe";
        var normalizedEmail = adminEmail.ToUpperInvariant();
        var admin = await userManager.Users.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.NormalizedEmail == normalizedEmail, cancellationToken);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, TenantSlug = tenant.Slug,
                UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, IsActive = true,
                FirstName = configuration["IdentityBootstrap:Admin:FirstName"] ?? "Platform",
                LastName = configuration["IdentityBootstrap:Admin:LastName"] ?? "Admin"
            };
            EnsureSucceeded(await userManager.CreateAsync(admin, adminPassword));
        }
        else if (configuration.GetValue("IdentityBootstrap:Admin:ResetPassword", false))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(admin);
            EnsureSucceeded(await userManager.ResetPasswordAsync(admin, token, adminPassword));
            admin.IsActive = true;
            await userManager.UpdateAsync(admin);
            logger.LogWarning("Bootstrap password reset is enabled for tenant {TenantSlug}; disable IdentityBootstrap:Admin:ResetPassword outside local/demo environments.", tenant.Slug);
        }

        var roleStorageName = TenantRoleNameCodec.ToStorageName(tenant.Id, "Admin");
        var normalizedRole = roleStorageName.ToUpperInvariant();
        var adminRole = await roleManager.Roles.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.NormalizedName == normalizedRole, cancellationToken);

        if (adminRole is null)
        {
            adminRole = new ApplicationRole
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, Name = roleStorageName,
                Description = "Tenant administrator"
            };
            EnsureSucceeded(await roleManager.CreateAsync(adminRole));
        }

        if (!await userManager.IsInRoleAsync(admin, roleStorageName))
            EnsureSucceeded(await userManager.AddToRoleAsync(admin, roleStorageName));

        var permissions = configuration.GetSection("IdentityBootstrap:Admin:Permissions").Get<string[]>() ?? QualifyAiPermissions.All;
        var existingPermissions = await dbContext.UserPermissions
            .Where(x => x.TenantId == tenant.Id && x.UserId == admin.Id)
            .Select(x => x.Permission).ToListAsync(cancellationToken);
        var missingPermissions = permissions
            .Where(x => !existingPermissions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Select(x => new UserPermission { TenantId = tenant.Id, UserId = admin.Id, Permission = x })
            .ToArray();
        if (missingPermissions.Length > 0)
        {
            dbContext.UserPermissions.AddRange(missingPermissions);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("Identity bootstrap ready for tenant {TenantSlug}; admin {AdminEmail}; plan {Plan}.", tenant.Slug, adminEmail, license.Plan);

        await EnsureFusionFleetAsync(dbContext, userManager, roleManager, outbox, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureFusionFleetAsync(
        IdentityDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        IOutboxWriter outbox,
        CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("FusionFleetSeed:Enabled", false))
        {
            logger.LogInformation("FusionFleet seed is disabled.");
            return;
        }

        var tenantId = ParseOptionalTenantId(configuration["FusionFleetSeed:Tenant:Id"])
            ?? throw new InvalidOperationException("FusionFleetSeed:Tenant:Id is required.");

        var tenantSlug = configuration["FusionFleetSeed:Tenant:Slug"]?.Trim().ToLowerInvariant()
            ?? "fusionfleet";
        var tenantName = configuration["FusionFleetSeed:Tenant:Name"]?.Trim() ?? "FusionFleet";
        var contactEmail = configuration["FusionFleetSeed:Tenant:ContactEmail"]?.Trim().ToLowerInvariant()
            ?? "admin@fusionfleet.local";

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Slug == tenantSlug, cancellationToken);
        var tenantCreated = false;

        if (tenant is null)
        {
            tenant = Tenant.Create(tenantId, tenantName, tenantSlug, contactEmail);
            await dbContext.Tenants.AddAsync(tenant, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            tenantCreated = true;
            logger.LogInformation("FusionFleet tenant created: {TenantSlug} ({TenantId}).", tenant.Slug, tenant.Id);
        }
        else
        {
            logger.LogInformation("FusionFleet tenant already exists: {TenantSlug} ({TenantId}).", tenant.Slug, tenant.Id);
        }

        var license = await dbContext.Licenses
            .Include(x => x.Modules)
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id, cancellationToken);
        var licenseCreated = false;

        if (license is null)
        {
            license = License.Create(
                tenant.Id,
                configuration["FusionFleetSeed:License:Plan"] ?? "Enterprise",
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddYears(1),
                configuration.GetValue("FusionFleetSeed:License:MaxUsers", 100),
                QualifyAiModules.Enterprise);

            await dbContext.Licenses.AddAsync(license, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            licenseCreated = true;
            logger.LogInformation("FusionFleet license created: {LicenseId}.", license.Id);
        }

        var adminEmail = configuration["FusionFleetSeed:Admin:Email"]?.Trim().ToLowerInvariant()
            ?? contactEmail;
        var adminPassword = configuration["FusionFleetSeed:Admin:Password"] ?? "FusionFleet123!ChangeMe";
        var normalizedEmail = adminEmail.ToUpperInvariant();

        var admin = await userManager.Users.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                TenantSlug = tenant.Slug,
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                IsActive = true,
                FirstName = configuration["FusionFleetSeed:Admin:FirstName"] ?? "FusionFleet",
                LastName = configuration["FusionFleetSeed:Admin:LastName"] ?? "Admin"
            };

            EnsureSucceeded(await userManager.CreateAsync(admin, adminPassword));
            logger.LogInformation("FusionFleet admin created: {AdminEmail}.", adminEmail);
        }
        else if (configuration.GetValue("FusionFleetSeed:Admin:ResetPassword", false))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(admin);
            EnsureSucceeded(await userManager.ResetPasswordAsync(admin, token, adminPassword));
            admin.IsActive = true;
            EnsureSucceeded(await userManager.UpdateAsync(admin));
        }

        var roleStorageName = TenantRoleNameCodec.ToStorageName(tenant.Id, "Admin");
        var normalizedRole = roleStorageName.ToUpperInvariant();
        var adminRole = await roleManager.Roles.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.NormalizedName == normalizedRole,
            cancellationToken);

        if (adminRole is null)
        {
            adminRole = new ApplicationRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = roleStorageName,
                Description = "Tenant administrator"
            };
            EnsureSucceeded(await roleManager.CreateAsync(adminRole));
        }

        if (!await userManager.IsInRoleAsync(admin, roleStorageName))
            EnsureSucceeded(await userManager.AddToRoleAsync(admin, roleStorageName));

        var permissions = configuration.GetSection("IdentityBootstrap:Admin:Permissions")
            .Get<string[]>() ?? QualifyAiPermissions.All;

        var existingPermissions = await dbContext.UserPermissions
            .Where(x => x.TenantId == tenant.Id && x.UserId == admin.Id)
            .Select(x => x.Permission)
            .ToListAsync(cancellationToken);

        var missingPermissions = permissions
            .Where(x => !existingPermissions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Select(x => new UserPermission
            {
                TenantId = tenant.Id,
                UserId = admin.Id,
                Permission = x
            })
            .ToArray();

        if (missingPermissions.Length > 0)
        {
            dbContext.UserPermissions.AddRange(missingPermissions);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (tenantCreated || licenseCreated)
        {
            var snapshotAtUtc = DateTime.UtcNow;
            if (tenantCreated)
            {
                outbox.Add(new TenantCreatedIntegrationEvent(
                    Guid.NewGuid(), snapshotAtUtc, tenant.Id, tenant.Slug, tenant.Name, tenant.ContactEmail));
            }

            if (licenseCreated)
            {
                outbox.Add(new TenantLicenseChangedIntegrationEvent(
                    Guid.NewGuid(), snapshotAtUtc, tenant.Id, tenant.Slug, license.Id, license.Plan,
                    license.Status.ToString().ToLowerInvariant(), license.MaxUsers, license.StartsAtUtc,
                    license.ExpiresAtUtc, license.Version, license.Modules.Select(x => x.Code).ToArray()));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation(
            "FusionFleet seed ready: tenant {TenantSlug}; admin {AdminEmail}; plan {Plan}.",
            tenant.Slug, adminEmail, license.Plan);
    }

    private static async Task MigrateDatabaseAsync(
        IdentityDbContext dbContext,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (SqlException ex) when (ex.Number == 1801 && attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }
    }

    private static Guid? ParseOptionalTenantId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Guid.TryParse(value, out var id)) return id;
        throw new InvalidOperationException($"IdentityBootstrap:Tenant:Id must be a valid GUID. Value='{value}'.");
    }

    private async Task EnsureAdminUiClientAsync(
        IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        var clientId = configuration["IdentityBootstrap:Admin:ClientId"]?.Trim()
            ?? "leadsai-admin";

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = "LeadsAI Admin UI",
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
                Permissions.Prefixes.Scope + "leadsai-api"
            }
        };

        var existing = await applicationManager.FindByClientIdAsync(clientId, cancellationToken);
        if (existing is null)
        {
            await applicationManager.CreateAsync(descriptor, cancellationToken);
            return;
        }

        await applicationManager.UpdateAsync(existing, descriptor, cancellationToken);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
