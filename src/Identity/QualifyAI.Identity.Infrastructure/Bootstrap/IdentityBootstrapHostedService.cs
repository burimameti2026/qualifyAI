using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.Contracts.Identity;
using QualifyAI.Identity.Domain.Licensing;
using QualifyAI.Identity.Domain.Tenants;
using QualifyAI.Identity.Persistence.SqlServer.Identity;
using QualifyAI.Identity.Persistence.SqlServer;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QualifyAI.Identity.Infrastructure.Bootstrap;

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

        await dbContext.Database.MigrateAsync(cancellationToken);
        await EnsureAdminUiClientAsync(applicationManager, cancellationToken);

        var legacyTenants = await dbContext.Tenants
            .Where(x => x.Slug == "demo" || x.Slug == "renova")
            .ToListAsync(cancellationToken);

        if (legacyTenants.Count > 0)
        {
            dbContext.Tenants.RemoveRange(legacyTenants);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Removed {Count} legacy demo/Renova identity tenants.", legacyTenants.Count);
        }

        var tenantSlug = configuration["IdentityBootstrap:Tenant:Slug"]?.Trim().ToLowerInvariant() ?? "master";
        var tenantName = configuration["IdentityBootstrap:Tenant:Name"]?.Trim() ?? "QualifyAI Master";
        var contactEmail = configuration["IdentityBootstrap:Tenant:ContactEmail"]?.Trim().ToLowerInvariant() ?? "admin@qualifyai.local";
        var configuredTenantId = ParseOptionalTenantId(configuration["IdentityBootstrap:Tenant:Id"]);

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Slug == tenantSlug, cancellationToken);
        if (tenant is null)
        {
            tenant = configuredTenantId.HasValue
                ? Tenant.Create(configuredTenantId.Value, tenantName, tenantSlug, contactEmail)
                : Tenant.Create(tenantName, tenantSlug, contactEmail);
            await dbContext.Tenants.AddAsync(tenant, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Provisioned master tenant {TenantSlug} ({TenantId}).", tenant.Slug, tenant.Id);
        }
        else if (configuredTenantId.HasValue && tenant.Id != configuredTenantId.Value)
        {
            logger.LogWarning("Configured master tenant id is only used when creating the tenant.");
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
            logger.LogInformation("Provisioned master license {LicenseId}.", license.Id);
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
                FirstName = configuration["IdentityBootstrap:Admin:FirstName"] ?? "Master",
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
                Description = "Master platform administrator"
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

        logger.LogInformation("Master identity bootstrap ready; admin {AdminEmail}; plan {Plan}.", adminEmail, license.Plan);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static Guid? ParseOptionalTenantId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Guid.TryParse(value, out var id)) return id;
        throw new InvalidOperationException($"IdentityBootstrap:Tenant:Id must be a valid GUID. Value='{value}'.");
    }

    private static async Task EnsureAdminUiClientAsync(IOpenIddictApplicationManager applicationManager, CancellationToken cancellationToken)
    {
        const string clientId = "qualifyai-admin";
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
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
                Permissions.Prefixes.Scope + "qualifyai-api"
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
