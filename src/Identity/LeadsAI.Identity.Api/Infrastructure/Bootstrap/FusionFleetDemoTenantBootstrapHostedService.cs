using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Messaging.Outbox;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.Contracts.Identity;
using LeadsAI.Identity.Domain.Licensing;
using LeadsAI.Identity.Domain.Tenants;
using LeadsAI.Identity.Persistence.SqlServer;
using LeadsAI.Identity.Persistence.SqlServer.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace LeadsAI.Identity.Infrastructure.Bootstrap;

public sealed class FusionFleetDemoTenantBootstrapHostedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<FusionFleetDemoTenantBootstrapHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("FusionFleetSeed:Enabled"))
            return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var outbox = scope.ServiceProvider.GetRequiredService<IOutboxWriter>();

        var tenantId = Guid.Parse(configuration["FusionFleetSeed:Tenant:Id"]!);
        var slug = configuration["FusionFleetSeed:Tenant:Slug"]?.Trim().ToLowerInvariant() ?? "fusionfleet";
        var name = configuration["FusionFleetSeed:Tenant:Name"]?.Trim() ?? "FusionFleet";
        var contactEmail = configuration["FusionFleetSeed:Tenant:ContactEmail"]?.Trim().ToLowerInvariant() ?? "admin@fusionfleet.local";

        var tenant = await db.Tenants.FirstOrDefaultAsync(x => x.Slug == slug, cancellationToken);\n        var tenantCreated = false;\n        var licenseCreated = false;
        if (tenant is null)
        {
            tenant = Tenant.Create(tenantId, name, slug, contactEmail);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken);
        }

        var license = await db.Licenses.Include(x => x.Modules)
            .FirstOrDefaultAsync(x => x.TenantId == tenant.Id, cancellationToken);

        if (license is null)
        {
            var modules = configuration.GetSection("FusionFleetSeed:License:Modules").Get<string[]>()
                ?? QualifyAiModules.Enterprise;

            license = License.Create(
                tenant.Id,
                configuration["FusionFleetSeed:License:Plan"] ?? "Enterprise",
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow.AddYears(1),
                configuration.GetValue("FusionFleetSeed:License:MaxUsers", 100),
                modules);

            db.Licenses.Add(license);
            await db.SaveChangesAsync(cancellationToken);
        }

        var snapshotAtUtc = DateTime.UtcNow;
        outbox.Add(new TenantCreatedIntegrationEvent(
            Guid.NewGuid(), snapshotAtUtc, tenant.Id, tenant.Slug, tenant.Name, tenant.ContactEmail));
        outbox.Add(new TenantLicenseChangedIntegrationEvent(
            Guid.NewGuid(), snapshotAtUtc, tenant.Id, tenant.Slug, license.Id, license.Plan,
            license.Status.ToString().ToLowerInvariant(), license.MaxUsers, license.StartsAtUtc,
            license.ExpiresAtUtc, license.Version, license.Modules.Select(x => x.Code).ToArray()));
        await db.SaveChangesAsync(cancellationToken);

        var adminEmail = configuration["FusionFleetSeed:Admin:Email"]?.Trim().ToLowerInvariant() ?? contactEmail;
        var adminPassword = configuration["FusionFleetSeed:Admin:Password"] ?? "FusionFleet123!ChangeMe";
        var admin = await userManager.Users.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.NormalizedEmail == adminEmail.ToUpperInvariant(),
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
        }

        var roleName = TenantRoleNameCodec.ToStorageName(tenant.Id, "Admin");
        var role = await roleManager.Roles.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.NormalizedName == roleName.ToUpperInvariant(),
            cancellationToken);

        if (role is null)
        {
            role = new ApplicationRole
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                Name = roleName,
                Description = "FusionFleet tenant administrator"
            };
            EnsureSucceeded(await roleManager.CreateAsync(role));
        }

        if (!await userManager.IsInRoleAsync(admin, roleName))
            EnsureSucceeded(await userManager.AddToRoleAsync(admin, roleName));

        var permissions = configuration.GetSection("FusionFleetSeed:Admin:Permissions").Get<string[]>()
            ?? QualifyAiPermissions.All;
        var existingPermissions = await db.UserPermissions
            .Where(x => x.TenantId == tenant.Id && x.UserId == admin.Id)
            .Select(x => x.Permission)
            .ToListAsync(cancellationToken);

        var missingPermissions = permissions
            .Where(x => !existingPermissions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Select(x => new UserPermission { TenantId = tenant.Id, UserId = admin.Id, Permission = x })
            .ToArray();

        if (missingPermissions.Length > 0)
        {
            db.UserPermissions.AddRange(missingPermissions);
            await db.SaveChangesAsync(cancellationToken);
        }

        logger.LogInformation("FusionFleet demo tenant ready: {TenantSlug} ({TenantId}) with admin {AdminEmail}.",
            tenant.Slug, tenant.Id, adminEmail);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static async Task EnsureAdminAsync(
        IdentityDbContext db,
        UserManager<ApplicationUser> userManager,
        RoleManager<ApplicationRole> roleManager,
        Tenant tenant,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var adminEmail = configuration["FusionFleetSeed:Admin:Email"]?.Trim().ToLowerInvariant() ?? tenant.ContactEmail;
        var adminPassword = configuration["FusionFleetSeed:Admin:Password"] ?? "FusionFleet123!ChangeMe";
        var admin = await userManager.Users.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.NormalizedEmail == adminEmail.ToUpperInvariant(),
            cancellationToken);

        if (admin is null)
        {
            admin = new ApplicationUser
            {
                Id = Guid.NewGuid(), TenantId = tenant.Id, TenantSlug = tenant.Slug,
                UserName = adminEmail, Email = adminEmail, EmailConfirmed = true, IsActive = true,
                FirstName = configuration["FusionFleetSeed:Admin:FirstName"] ?? "FusionFleet",
                LastName = configuration["FusionFleetSeed:Admin:LastName"] ?? "Admin"
            };
            EnsureSucceeded(await userManager.CreateAsync(admin, adminPassword));
        }

        var roleName = TenantRoleNameCodec.ToStorageName(tenant.Id, "Admin");
        var role = await roleManager.Roles.FirstOrDefaultAsync(
            x => x.TenantId == tenant.Id && x.NormalizedName == roleName.ToUpperInvariant(),
            cancellationToken);

        if (role is null)
        {
            role = new ApplicationRole { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = roleName, Description = "FusionFleet tenant administrator" };
            EnsureSucceeded(await roleManager.CreateAsync(role));
        }

        if (!await userManager.IsInRoleAsync(admin, roleName))
            EnsureSucceeded(await userManager.AddToRoleAsync(admin, roleName));

        var permissions = configuration.GetSection("FusionFleetSeed:Admin:Permissions").Get<string[]>() ?? QualifyAiPermissions.All;
        var existingPermissions = await db.UserPermissions.Where(x => x.TenantId == tenant.Id && x.UserId == admin.Id).Select(x => x.Permission).ToListAsync(cancellationToken);
        var missingPermissions = permissions.Where(x => !existingPermissions.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Select(x => new UserPermission { TenantId = tenant.Id, UserId = admin.Id, Permission = x }).ToArray();

        if (missingPermissions.Length > 0)
        {
            db.UserPermissions.AddRange(missingPermissions);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded) return;
        throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}
