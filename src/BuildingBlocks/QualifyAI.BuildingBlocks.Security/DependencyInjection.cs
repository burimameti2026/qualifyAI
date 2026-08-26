using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentTenant,CurrentTenant>();
        services.AddSingleton<IAuthorizationHandler,PermissionAuthorizationHandler>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.Authority = authority;
                o.MapInboundClaims = false;
                o.RequireHttpsMetadata = false;
                o.Audience = "qualifyai-api";
            });
        services.AddAuthorization();
        return services;
    }
}
