using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Infrastructure.Authentication;
using QualifyAI.Identity.Infrastructure.Identity;
using QualifyAI.Identity.Infrastructure.Persistence;
using QualifyAI.Identity.Infrastructure.Tenants;

namespace QualifyAI.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(o =>
        {
            o.UseSqlServer(configuration.GetConnectionString("IdentityDb"));
            o.UseOpenIddict();
        });

        services.AddIdentityCore<ApplicationUser>(o =>
        {
            o.Password.RequiredLength=10;
            o.Password.RequireDigit=true;
            o.Password.RequireLowercase=true;
            o.Password.RequireUppercase=true;
            o.Password.RequireNonAlphanumeric=true;
            o.Lockout.MaxFailedAccessAttempts=5;
            o.Lockout.DefaultLockoutTimeSpan=TimeSpan.FromMinutes(15);
            o.User.RequireUniqueEmail=false; // uniqueness is enforced per tenant.
        })
        .AddRoles<ApplicationRole>()
        .AddEntityFrameworkStores<IdentityDbContext>()
        .AddSignInManager()
        .AddDefaultTokenProviders();

        services.AddOpenIddict()
            .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
            .AddServer(o =>
            {
                o.SetIssuer(new Uri(configuration["Identity:Issuer"] ?? "http://identity-api:8080"));
                o.SetTokenEndpointUris("/connect/token");
                o.AllowPasswordFlow();
                o.AllowRefreshTokenFlow();
                o.RegisterScopes("qualifyai-api","openid","profile","email","offline_access");
                o.DisableAccessTokenEncryption();
                o.AddDevelopmentEncryptionCertificate();
                o.AddDevelopmentSigningCertificate();
                o.UseAspNetCore().EnableTokenEndpointPassthrough();
            })
            .AddValidation(o =>
            {
                o.UseLocalServer();
                o.UseAspNetCore();
            });

        services.AddScoped<IAccountService,AccountService>();
        services.AddHttpClient<ITenantDirectoryClient,TenantDirectoryClient>(client =>
            client.BaseAddress=new Uri(configuration["Services:TenantManagement"] ?? "http://business-api:8080/internal"));

        services.AddHostedService<IdentityBootstrapHostedService>();
        return services;
    }
}
