using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Application.AccessControl;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Application.Clients;
using QualifyAI.Identity.Application.Licensing;
using QualifyAI.Identity.Application.Tenants.ProvisionTenant;
using QualifyAI.Identity.Infrastructure.Authentication;
using QualifyAI.Identity.Infrastructure.Bootstrap;
using QualifyAI.Identity.Infrastructure.Clients;
using QualifyAI.Identity.Infrastructure.Licensing;
using QualifyAI.Identity.Infrastructure.Messaging;
using QualifyAI.Identity.Infrastructure.Tenants.ProvisionTenant;
using QualifyAI.Identity.Persistence.SqlServer;
using QualifyAI.Identity.Persistence.SqlServer.Identity;
using QualifyAI.Identity.Persistence.SqlServer.Repositories;

namespace QualifyAI.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration, bool allowDevelopmentHttp = false)
    {
        services.AddDbContext<IdentityDbContext>(options => { options.UseSqlServer(configuration.GetConnectionString("IdentityDb"), sql => sql.EnableRetryOnFailure()); options.UseOpenIddict(); });
        services.AddIdentityCore<ApplicationUser>(options => { options.Password.RequiredLength = 10; options.Password.RequireDigit = true; options.Password.RequireLowercase = true; options.Password.RequireUppercase = true; options.Password.RequireNonAlphanumeric = true; options.Lockout.MaxFailedAccessAttempts = 5; options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); options.User.RequireUniqueEmail = false; }).AddRoles<ApplicationRole>().AddEntityFrameworkStores<IdentityDbContext>().AddSignInManager().AddDefaultTokenProviders();
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>()).AddServer(options => { options.SetIssuer(new Uri(configuration["Identity:Issuer"] ?? "http://identity-api:8080")); options.SetTokenEndpointUris("/connect/token"); options.AllowPasswordFlow(); options.AllowRefreshTokenFlow(); options.AllowClientCredentialsFlow(); options.RegisterScopes("qualifyai-api", "openid", "profile", "email", "offline_access"); options.DisableAccessTokenEncryption(); options.AddDevelopmentEncryptionCertificate(); options.AddDevelopmentSigningCertificate(); var aspNetCore = options.UseAspNetCore().EnableTokenEndpointPassthrough(); if (allowDevelopmentHttp) aspNetCore.DisableTransportSecurityRequirement(); }).AddValidation(options => { options.UseLocalServer(); options.UseAspNetCore(); });
        services.AddAuthentication(options => { options.DefaultAuthenticateScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme; options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme; options.DefaultForbidScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme; });
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ILicenseRepository, LicenseRepository>();
        services.AddScoped<IClientApplicationRepository, ClientApplicationRepository>();
        services.AddScoped<IIdentityUnitOfWork, IdentityUnitOfWork>();
        services.AddScoped<IOutboxWriter, IdentityOutboxWriter>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<ITenantEntitlementService, TenantEntitlementService>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<ISecurityLifecycleService, SecurityLifecycleService>();
        services.AddScoped<IUserPermissionReader, UserPermissionReader>();
        services.AddScoped<IClientCredentialStore, OpenIddictClientCredentialStore>();
        services.AddScoped<IAccessControlRepository, AccessControlRepository>();
        services.AddHostedService<IdentityBootstrapHostedService>();
        services.AddHostedService<IdentityOutboxPublisherHostedService>();
        return services;
    }
}
