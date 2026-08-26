using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using OpenIddict.Abstractions;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Application.Permissions;
using QualifyAI.Identity.Infrastructure.Identity;
using QualifyAI.Identity.Infrastructure.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QualifyAI.Identity.Infrastructure.Authentication;

public sealed class IdentityBootstrapHostedService(IServiceProvider services):IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope=services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await db.Database.EnsureCreatedAsync(ct);

        var applications=scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        if(await applications.FindByClientIdAsync("qualifyai-admin",ct) is null)
        {
            await applications.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId="qualifyai-admin",
                ClientType=ClientTypes.Public,
                DisplayName="QualifyAI Admin",
                ConsentType=ConsentTypes.Implicit,
                Permissions=
                {
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.Password,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Prefixes.Scope+"qualifyai-api"
                }
            },ct);
        }

        var tenantClient=scope.ServiceProvider.GetRequiredService<ITenantDirectoryClient>();
        TenantDirectoryEntry? tenant=null;
        for(var attempt=0;attempt<30 && tenant is null;attempt++)
        {
            try { tenant=await tenantClient.ResolveAsync("demo",ct); }
            catch when(!ct.IsCancellationRequested) { }
            if(tenant is null) await Task.Delay(TimeSpan.FromSeconds(2),ct);
        }
        if(tenant is null) throw new InvalidOperationException("Demo tenant could not be resolved from TenantManagement.");

        var users=scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roles=scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        var email="admin@demo.local";

        var admin=await users.Users.FirstOrDefaultAsync(x=>x.TenantId==tenant.Id && x.Email==email,ct);
        if(admin is null)
        {
            admin=new ApplicationUser
            {
                Id=Guid.NewGuid(),TenantId=tenant.Id,TenantSlug=tenant.Slug,
                UserName=email,Email=email,EmailConfirmed=true,
                FirstName="Demo",LastName="Administrator",IsActive=true
            };
            var create=await users.CreateAsync(admin,Environment.GetEnvironmentVariable("DEMO_ADMIN_PASSWORD") ?? "Admin123!");
            if(!create.Succeeded) throw new InvalidOperationException(string.Join("; ",create.Errors.Select(x=>x.Description)));
        }

        var adminRole=await roles.Roles.FirstOrDefaultAsync(x=>x.TenantId==tenant.Id && x.NormalizedName=="ADMIN",ct);
        if(adminRole is null)
        {
            adminRole=new ApplicationRole{Id=Guid.NewGuid(),TenantId=tenant.Id,Name="Admin",Description="Tenant administrator"};
            var roleCreate=await roles.CreateAsync(adminRole);
            if(!roleCreate.Succeeded) throw new InvalidOperationException(string.Join("; ",roleCreate.Errors.Select(x=>x.Description)));
        }

        if(!await users.IsInRoleAsync(admin,"Admin")) await users.AddToRoleAsync(admin,"Admin");

        if(!await db.UserPermissions.AnyAsync(x=>x.TenantId==tenant.Id && x.UserId==admin.Id,ct))
        {
            db.UserPermissions.AddRange(PlatformPermissions.All.Select(p=>new UserPermission{TenantId=tenant.Id,UserId=admin.Id,Permission=p}));
            await db.SaveChangesAsync(ct);
        }
    }
    public Task StopAsync(CancellationToken ct)=>Task.CompletedTask;
}
