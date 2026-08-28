using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.BuildingBlocks.Security.Tenancy;

namespace QualifyAI.BuildingBlocks.Security;

public static class DependencyInjection
{
    public static IServiceCollection AddQualifyAiResourceServer(this IServiceCollection services, IConfiguration configuration)
    {
        var authority = configuration["Identity:Authority"] ?? "http://identity-api:8080";
        var audience = configuration["Identity:Audience"] ?? "qualifyai-api";
        var requireHttps = configuration.GetValue("Identity:RequireHttps", false);

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenant, CurrentTenant>();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        services.AddSingleton<IAuthorizationPolicyProvider, QualifyAiAuthorizationPolicyProvider>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = authority;
                options.MapInboundClaims = false;
                options.RequireHttpsMetadata = requireHttps;
                options.Audience = audience;
            });

        services.AddAuthorization();
        return services;
    }
}
