using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Identity.Application;
using QualifyAI.Identity.Application.Licensing;
using QualifyAI.Identity.Application.Tenants.ProvisionTenant;
using QualifyAI.Identity.Domain.Licensing;
using QualifyAI.Identity.Domain.Tenants;
using QualifyAI.Identity.Persistence.SqlServer;
using QualifyAI.Identity.Persistence.SqlServer.Identity;

namespace QualifyAI.Identity.Infrastructure.Tenants.ProvisionTenant;

public sealed class TenantProvisioningService(
    IdentityDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<ApplicationRole> roles) : ITenantProvisioningService
{
    public async Task<ProvisionTenantResult> ProvisionAsync(ProvisionTenantCommand request, CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();
        var ownerEmail = request.OwnerEmail.Trim().ToLowerInvariant();
        var plan = LicensePlanCatalog.Get(request.Plan);
        var maxUsers = request.MaxUsers ?? plan.DefaultMaxUsers;
        if (maxUsers <= 0) throw new IdentityValidationException("maxUsers", "Max users must be greater than zero.");
        if (maxUsers > plan.DefaultMaxUsers && plan.Code != "enterprise") throw new IdentityValidationException("maxUsers", "Selected plan does not allow the requested user limit.");
        var modules = LicensePlanCatalog.ValidateModules(plan.Code, request.Modules ?? plan.Modules);

        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            if (await db.Tenants.AnyAsync(x => x.Slug == slug, cancellationToken))
                throw new IdentityConflictException($"Tenant slug '{slug}' already exists.");

            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

            var tenant = Tenant.Create(request.Name, slug, request.ContactEmail);
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(cancellationToken);

            var license = License.Create(tenant.Id, plan.Code, request.StartsAtUtc, request.ExpiresAtUtc, request.GracePeriodEndsAtUtc, maxUsers, modules);
            db.Licenses.Add(license);

            var owner = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                TenantId = tenant.Id,
                TenantSlug = tenant.Slug,
                UserName = ownerEmail,
                Email = ownerEmail,
                FirstName = request.OwnerFirstName.Trim(),
                LastName = request.OwnerLastName.Trim(),
                EmailConfirmed = true,
                IsActive = true
            };

            var createOwner = await users.CreateAsync(owner, request.OwnerPassword);
            EnsureSucceeded(createOwner);

            var roleName = TenantRoleNameCodec.ToStorageName(tenant.Id, "TenantOwner");
            var normalizedRoleName = roleName.ToUpperInvariant();
            var role = await roles.Roles.FirstOrDefaultAsync(x => x.TenantId == tenant.Id && x.NormalizedName == normalizedRoleName, cancellationToken);
            if (role is null)
            {
                role = new ApplicationRole { Id = Guid.NewGuid(), TenantId = tenant.Id, Name = roleName, Description = "Default tenant owner" };
                EnsureSucceeded(await roles.CreateAsync(role));
            }

            EnsureSucceeded(await users.AddToRoleAsync(owner, roleName));
            await users.UpdateSecurityStampAsync(owner);

            tenant.Activate();
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return new ProvisionTenantResult(
                tenant.Id,
                license.Id,
                owner.Id,
                tenant.Status.ToString(),
                license.GetEffectiveStatus(DateTime.UtcNow).ToString(),
                license.Plan,
                license.MaxUsers,
                license.Modules.Where(x => x.Enabled).Select(x => x.Code).ToArray());
        });
    }

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (result.Succeeded) return;
        throw new IdentityValidationException(result.Errors
            .GroupBy(x => string.IsNullOrWhiteSpace(x.Code) ? "identity" : x.Code)
            .ToDictionary(x => x.Key, x => x.Select(y => y.Description).Distinct().ToArray()));
    }
}
