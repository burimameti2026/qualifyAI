using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Infrastructure.Authentication;
using QualifyAI.Identity.Infrastructure.Bootstrap;
using QualifyAI.Identity.Infrastructure.Identity;
using QualifyAI.Identity.Infrastructure.Messaging;
using QualifyAI.Identity.Infrastructure.Persistence;
using QualifyAI.Identity.Infrastructure.Persistence.Repositories;

namespace QualifyAI.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<IdentityDbContext>(options =>
        {
            options.UseSqlServer(configuration.GetConnectionString("IdentityDb"));
            options.UseOpenIddict();
        });

        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
                options.User.RequireUniqueEmail = false;
            })
            .AddRoles<ApplicationRole>()
            .AddEntityFrameworkStores<IdentityDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();

        services.AddOpenIddict()
            .AddCore(options =>
                options.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>())
            .AddServer(options =>
            {
                options.SetIssuer(new Uri(
                    configuration["Identity:Issuer"] ?? "http://identity-api:8080"));
                options.SetTokenEndpointUris("/connect/token");
                options.AllowPasswordFlow();
                options.AllowRefreshTokenFlow();
                options.AllowClientCredentialsFlow();
                options.RegisterScopes(
                    "qualifyai-api",
                    "openid",
                    "profile",
                    "email",
                    "offline_access");
                options.DisableAccessTokenEncryption();
                options.AddDevelopmentEncryptionCertificate();
                options.AddDevelopmentSigningCertificate();
                options.UseAspNetCore().EnableTokenEndpointPassthrough();
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IOutboxWriter, IdentityOutboxWriter>();
        services.AddScoped<IAccountService, AccountService>();

        services.AddHostedService<IdentityBootstrapHostedService>();
        return services;
    }
}
