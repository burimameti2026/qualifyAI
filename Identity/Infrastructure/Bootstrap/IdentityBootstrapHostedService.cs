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

        var tenantSlug = configuration["IdentityBootstrap:Tenant:Slug"]?.Trim().ToLowerInvariant() ?? "demo";
        var tenantName = configuration["IdentityBootstrap:Tenant:Name"]?.Trim() ?? "QualifyAI Demo";
        var contactEmail = configuration["IdentityBootstrap:Tenant:ContactEmail"]?.Trim().ToLowerInvariant() ?? "admin@demo.local";

        var tenant = await dbContext.Tenants.FirstOrDefaultAsync(x => x.Slug == tenantSlug, cancellationToken);
        if (tenant is null)
        {
            tenant = Tenant.Create(tenantName, tenantSlug, contactEmail);
            await dbContext.Tenants.AddAsync(tenant, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Provisioned bootstrap tenant {TenantSlug} ({TenantId}).", tenant.Slug, tenant.Id);
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
            Guid.NewGuid(),
            snapshotAtUtc,
            tenant.Id,
            tenant.Slug,
            license.Id,
            license.Plan,
            license.Status.ToString().ToLowerInvariant(),
            license.MaxUsers,
            license.StartsAtUtc,
            license.ExpiresAtUtc,
            license.Version,
            license.Modules.Select(x => x.Code).ToArray()));
        await dbContext.SaveChangesAsync(cancellationToken);

        var adminEmail = configuration["IdentityBootstrap:Admin:Email"]?.Trim().ToLowerInvariant() ?? contactEmail;
        var adminPassword = configuration["IdentityBootstrap:Admin:Password"] ?? "Admin123!ChangeMe";
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
                FirstName = configuration["IdentityBootstrap:Admin:FirstName"] ?? "Platform",
                LastName = configuration["IdentityBootstrap:Admin:LastName"] ?? "Admin"
            };

            EnsureSucceeded(await userManager.CreateAsync(admin, adminPassword));
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

        var permissions = configuration
            .GetSection("IdentityBootstrap:Admin:Permissions")
            .Get<string[]>()
            ?? QualifyAiPermissions.All;

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

        logger.LogInformation(
            "Identity bootstrap ready for tenant {TenantSlug}; admin {AdminEmail}; plan {Plan}.",
            tenant.Slug,
            adminEmail,
            license.Plan);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureAdminUiClientAsync(
        IOpenIddictApplicationManager applicationManager,
        CancellationToken cancellationToken)
    {
        const string clientId = "qualifyai-admin";
        if (await applicationManager.FindByClientIdAsync(clientId, cancellationToken) is not null)
            return;

        await applicationManager.CreateAsync(new OpenIddictApplicationDescriptor
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
                Permissions.Prefixes.Scope + "qualifyai-api",
                Permissions.Prefixes.Scope + "profile",
                Permissions.Prefixes.Scope + "email"
            }
        }, cancellationToken);
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
