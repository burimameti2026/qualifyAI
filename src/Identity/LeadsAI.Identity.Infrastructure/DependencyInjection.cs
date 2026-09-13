using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;
using LeadsAI.BuildingBlocks.Messaging.Outbox;
using LeadsAI.Identity.Application.Abstractions.Persistence;
using LeadsAI.Identity.Application.AccessControl;
using LeadsAI.Identity.Application.Authentication;
using LeadsAI.Identity.Application.Clients;
using LeadsAI.Identity.Application.Licensing;
using LeadsAI.Identity.Application.Tenants.ProvisionTenant;
using LeadsAI.Identity.Infrastructure.Authentication;
using LeadsAI.Identity.Infrastructure.Bootstrap;
using LeadsAI.Identity.Infrastructure.Clients;
using LeadsAI.Identity.Infrastructure.Licensing;
using LeadsAI.Identity.Infrastructure.Messaging;
using LeadsAI.Identity.Infrastructure.Tenants.ProvisionTenant;
using LeadsAI.Identity.Persistence.SqlServer;
using LeadsAI.Identity.Persistence.SqlServer.Identity;
using LeadsAI.Identity.Persistence.SqlServer.Repositories;

namespace LeadsAI.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityInfrastructure(this IServiceCollection services, IConfiguration configuration, bool allowDevelopmentHttp = false)
    {
        services.AddDbContext<IdentityDbContext>(options => { options.UseSqlServer(configuration.GetConnectionString("IdentityDb"), sql => sql.EnableRetryOnFailure()); options.UseOpenIddict(); });
        services.AddIdentityCore<ApplicationUser>(options => { options.Password.RequiredLength = 10; options.Password.RequireDigit = true; options.Password.RequireLowercase = true; options.Password.RequireUppercase = true; options.Password.RequireNonAlphanumeric = true; options.Lockout.MaxFailedAccessAttempts = 5; options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15); options.User.RequireUniqueEmail = false; }).AddRoles<ApplicationRole>().AddEntityFrameworkStores<IdentityDbContext>().AddSignInManager().AddDefaultTokenProviders();
        services.AddOpenIddict().AddCore(options => options.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>()).AddServer(options => { options.SetIssuer(new Uri(configuration["Identity:Issuer"] ?? "http://identity-api:8080")); options.SetTokenEndpointUris("/connect/token"); options.AllowPasswordFlow(); options.AllowRefreshTokenFlow(); options.AllowClientCredentialsFlow(); options.RegisterScopes("leadsai-api", "qualifyai-api", "openid", "profile", "email", "offline_access"); options.DisableAccessTokenEncryption(); options.AddDevelopmentEncryptionCertificate(); options.AddDevelopmentSigningCertificate(); var aspNetCore = options.UseAspNetCore().EnableTokenEndpointPassthrough(); if (allowDevelopmentHttp) aspNetCore.DisableTransportSecurityRequirement(); }).AddValidation(options => { options.UseLocalServer(); options.UseAspNetCore(); });
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
