using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using QualifyAI.BuildingBlocks.Security.Claims;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Infrastructure.Identity;
using QualifyAI.Identity.Infrastructure.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QualifyAI.Identity.Api.Endpoints.Authentication;

public static class TokenEndpoint
{
    public static IEndpointRouteBuilder MapTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token",HandleAsync).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IdentityDbContext db,
        ITenantDirectoryClient tenants,
        CancellationToken ct)
    {
        var request=httpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict request is unavailable.");

        if(request.IsPasswordGrantType())
        {
            var tenantSlug=request.GetParameter("tenant")?.ToString()?.Trim().ToLowerInvariant();
            if(string.IsNullOrWhiteSpace(tenantSlug))
                return Results.BadRequest(new{error="tenant_required"});

            var tenant=await tenants.ResolveAsync(tenantSlug,ct);
            if(tenant is null || !tenant.IsActive)
                return Results.BadRequest(new{error="invalid_tenant"});

            var email=request.Username?.Trim().ToLowerInvariant() ?? "";
            var user=await userManager.Users.FirstOrDefaultAsync(
                x=>x.TenantId==tenant.Id && x.NormalizedEmail==email.ToUpper(),ct);

            if(user is null || !user.IsActive)
                return Results.Unauthorized();

            if(await userManager.IsLockedOutAsync(user))
                return Results.Unauthorized();

            if(!await userManager.CheckPasswordAsync(user,request.Password??""))
            {
                await userManager.AccessFailedAsync(user);
                return Results.Unauthorized();
            }

            await userManager.ResetAccessFailedCountAsync(user);

            if(user.TwoFactorEnabled)
            {
                var code=request.GetParameter("mfa_code")?.ToString();
                if(string.IsNullOrWhiteSpace(code))return Results.Json(new{error="mfa_required"},statusCode:401);
                var valid=await userManager.VerifyTwoFactorTokenAsync(user,TokenOptions.DefaultAuthenticatorProvider,code.Replace(" ","").Replace("-",""));
                if(!valid)return Results.Json(new{error="invalid_mfa_code"},statusCode:401);
            }

            var principal=await CreatePrincipalAsync(user,userManager,db,ct);
            principal.SetScopes(request.GetScopes().Union(["qualifyai-api","offline_access"]));
            principal.SetResources("qualifyai-api");
            return Results.SignIn(principal,authenticationScheme:OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if(request.IsRefreshTokenGrantType())
        {
            var result=await httpContext.AuthenticateAsync(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            var subject=result.Principal?.GetClaim(Claims.Subject);
            if(!Guid.TryParse(subject,out var userId)) return Results.Unauthorized();

            var user=await userManager.Users.FirstOrDefaultAsync(x=>x.Id==userId,ct);
            if(user is null || !user.IsActive) return Results.Unauthorized();

            var principal=await CreatePrincipalAsync(user,userManager,db,ct);
            principal.SetScopes(request.GetScopes());
            principal.SetResources("qualifyai-api");
            return Results.SignIn(principal,authenticationScheme:OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Results.BadRequest(new{error="unsupported_grant_type"});
    }

    private static async Task<ClaimsPrincipal> CreatePrincipalAsync(
        ApplicationUser user,UserManager<ApplicationUser> userManager,IdentityDbContext db,CancellationToken ct)
    {
        var identity=new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);

        identity.SetClaim(Claims.Subject,user.Id.ToString());
        identity.SetClaim(Claims.Email,user.Email);
        identity.SetClaim(Claims.Name,$"{user.FirstName} {user.LastName}".Trim());
        identity.SetClaim(QualifyAiClaimTypes.TenantId,user.TenantId.ToString());
        identity.SetClaim(QualifyAiClaimTypes.TenantSlug,user.TenantSlug);

        foreach(var role in await userManager.GetRolesAsync(user))
            identity.AddClaim(new Claim(Claims.Role,role));

        var permissions=await db.UserPermissions.AsNoTracking()
            .Where(x=>x.TenantId==user.TenantId && x.UserId==user.Id)
            .Select(x=>x.Permission).ToListAsync(ct);
        foreach(var permission in permissions)
            identity.AddClaim(new Claim(QualifyAiClaimTypes.Permission,permission));

        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Name or Claims.Email or Claims.Role
                => [Destinations.AccessToken,Destinations.IdentityToken],
            _ => [Destinations.AccessToken]
        });

        return new ClaimsPrincipal(identity);
    }
}
