using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using QualifyAI.BuildingBlocks.Security.Claims;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Application.Authentication.ResolveClientAccess;
using QualifyAI.Identity.Application.Authentication.ResolveTenantAccess;
using QualifyAI.Identity.Infrastructure.Identity;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace QualifyAI.Identity.Api.Endpoints.Authentication;

public static class TokenEndpoint
{
    public static IEndpointRouteBuilder MapTokenEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/connect/token", HandleAsync).AllowAnonymous();
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext httpContext,
        UserManager<ApplicationUser> userManager,
        IUserPermissionReader permissionReader,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var request = httpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict request is unavailable.");

        if (request.IsPasswordGrantType())
        {
            return await HandlePasswordGrantAsync(
                request,
                userManager,
                permissionReader,
                sender,
                cancellationToken);
        }

        if (request.IsRefreshTokenGrantType())
        {
            return await HandleRefreshGrantAsync(
                httpContext,
                request,
                userManager,
                permissionReader,
                sender,
                cancellationToken);
        }

        if (request.IsClientCredentialsGrantType())
        {
            return await HandleClientCredentialsGrantAsync(
                request,
                sender,
                cancellationToken);
        }

        return Results.BadRequest(new { error = "unsupported_grant_type" });
    }

    private static async Task<IResult> HandlePasswordGrantAsync(
        OpenIddictRequest request,
        UserManager<ApplicationUser> userManager,
        IUserPermissionReader permissionReader,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var tenantSlug = request.GetParameter("tenant")?.ToString()?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(tenantSlug))
            return Results.BadRequest(new { error = "tenant_required" });

        var access = await sender.Send(
            new ResolveTenantAccessQuery(tenantSlug),
            cancellationToken);

        if (access is null || !access.TenantActive)
            return Results.BadRequest(new { error = "invalid_tenant" });

        if (!access.LicenseUsable)
            return Results.Json(new { error = "license_inactive" }, statusCode: StatusCodes.Status403Forbidden);

        var normalizedEmail = (request.Username ?? string.Empty).Trim().ToUpperInvariant();
        var user = await userManager.Users.FirstOrDefaultAsync(
            x => x.TenantId == access.TenantId && x.NormalizedEmail == normalizedEmail,
            cancellationToken);

        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
            return Results.Unauthorized();

        if (!await userManager.CheckPasswordAsync(user, request.Password ?? string.Empty))
        {
            await userManager.AccessFailedAsync(user);
            return Results.Unauthorized();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var mfaResult = await ValidateMfaAsync(request, user, userManager);
        if (mfaResult is not null)
            return mfaResult;

        var principal = await CreateUserPrincipalAsync(
            user,
            access,
            userManager,
            permissionReader,
            cancellationToken);

        principal.SetScopes(request.GetScopes().Union(["qualifyai-api", "offline_access"]));
        principal.SetResources("qualifyai-api");

        return Results.SignIn(
            principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> HandleRefreshGrantAsync(
        HttpContext httpContext,
        OpenIddictRequest request,
        UserManager<ApplicationUser> userManager,
        IUserPermissionReader permissionReader,
        ISender sender,
        CancellationToken cancellationToken)
    {
        var authentication = await httpContext.AuthenticateAsync(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

        var subject = authentication.Principal?.GetClaim(Claims.Subject);
        if (!Guid.TryParse(subject, out var userId))
            return Results.Unauthorized();

        var user = await userManager.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);
        if (user is null || !user.IsActive || await userManager.IsLockedOutAsync(user))
            return Results.Unauthorized();

        var access = await sender.Send(
            new ResolveTenantAccessQuery(user.TenantSlug),
            cancellationToken);

        if (access is null || !access.TenantActive || !access.LicenseUsable)
            return Results.Unauthorized();

        var principal = await CreateUserPrincipalAsync(
            user,
            access,
            userManager,
            permissionReader,
            cancellationToken);

        principal.SetScopes(request.GetScopes());
        principal.SetResources("qualifyai-api");

        return Results.SignIn(
            principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> HandleClientCredentialsGrantAsync(
        OpenIddictRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId))
            return Results.BadRequest(new { error = "invalid_client" });

        var access = await sender.Send(
            new ResolveClientAccessQuery(request.ClientId),
            cancellationToken);

        if (access is null)
            return Results.Unauthorized();

        if (!access.TenantActive)
            return Results.Json(new { error = "tenant_inactive" }, statusCode: StatusCodes.Status403Forbidden);

        if (!access.LicenseUsable)
            return Results.Json(new { error = "license_inactive" }, statusCode: StatusCodes.Status403Forbidden);

        var requestedScopes = request.GetScopes().ToArray();
        var unauthorizedScopes = requestedScopes
            .Where(scope => !access.AllowedScopes.Contains(scope, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        if (unauthorizedScopes.Length > 0)
            return Results.BadRequest(new { error = "invalid_scope", scopes = unauthorizedScopes });

        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);

        identity.SetClaim(Claims.Subject, access.ClientId);
        identity.SetClaim(Claims.ClientId, access.ClientId);
        identity.SetClaim(Claims.Name, access.DisplayName);

        if (access.TenantId.HasValue)
        {
            identity.SetClaim(QualifyAiClaimTypes.TenantId, access.TenantId.Value.ToString());
            identity.SetClaim(QualifyAiClaimTypes.TenantSlug, access.TenantSlug);
            identity.SetClaim(QualifyAiClaimTypes.LicensePlan, access.LicensePlan);
            identity.SetClaim(QualifyAiClaimTypes.LicenseStatus, access.LicenseStatus);
            identity.SetClaim(QualifyAiClaimTypes.LicenseVersion, access.LicenseVersion?.ToString());

            foreach (var module in access.Modules)
                identity.AddClaim(new Claim(QualifyAiClaimTypes.Module, module));
        }

        identity.SetDestinations(_ => [Destinations.AccessToken]);

        var principal = new ClaimsPrincipal(identity);
        principal.SetScopes(requestedScopes.Length > 0 ? requestedScopes : access.AllowedScopes);
        principal.SetResources("qualifyai-api");

        return Results.SignIn(
            principal,
            authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult?> ValidateMfaAsync(
        OpenIddictRequest request,
        ApplicationUser user,
        UserManager<ApplicationUser> userManager)
    {
        if (!user.TwoFactorEnabled)
            return null;

        var code = request.GetParameter("mfa_code")?.ToString();
        if (string.IsNullOrWhiteSpace(code))
            return Results.Json(new { error = "mfa_required" }, statusCode: StatusCodes.Status401Unauthorized);

        var valid = await userManager.VerifyTwoFactorTokenAsync(
            user,
            TokenOptions.DefaultAuthenticatorProvider,
            code.Replace(" ", string.Empty).Replace("-", string.Empty));

        return valid
            ? null
            : Results.Json(new { error = "invalid_mfa_code" }, statusCode: StatusCodes.Status401Unauthorized);
    }

    private static async Task<ClaimsPrincipal> CreateUserPrincipalAsync(
        ApplicationUser user,
        TenantAccessSnapshot access,
        UserManager<ApplicationUser> userManager,
        IUserPermissionReader permissionReader,
        CancellationToken cancellationToken)
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Name,
            Claims.Role);

        identity.SetClaim(Claims.Subject, user.Id.ToString());
        identity.SetClaim(Claims.Email, user.Email);
        identity.SetClaim(Claims.Name, $"{user.FirstName} {user.LastName}".Trim());
        identity.SetClaim(QualifyAiClaimTypes.TenantId, access.TenantId.ToString());
        identity.SetClaim(QualifyAiClaimTypes.TenantSlug, access.TenantSlug);
        identity.SetClaim(QualifyAiClaimTypes.LicensePlan, access.LicensePlan);
        identity.SetClaim(QualifyAiClaimTypes.LicenseStatus, access.LicenseStatus);
        identity.SetClaim(QualifyAiClaimTypes.LicenseVersion, access.LicenseVersion.ToString());

        var storageRoles = await userManager.GetRolesAsync(user);
        foreach (var storageRole in storageRoles)
        {
            var displayRole = TenantRoleNameCodec.ToDisplayName(user.TenantId, storageRole);
            identity.AddClaim(new Claim(Claims.Role, displayRole));
        }

        var permissions = await permissionReader.ListAsync(
            user.TenantId,
            user.Id,
            cancellationToken);

        foreach (var permission in permissions)
            identity.AddClaim(new Claim(QualifyAiClaimTypes.Permission, permission));

        foreach (var module in access.Modules)
            identity.AddClaim(new Claim(QualifyAiClaimTypes.Module, module));

        identity.SetDestinations(claim => claim.Type switch
        {
            Claims.Name or Claims.Email or Claims.Role
                => [Destinations.AccessToken, Destinations.IdentityToken],
            _ => [Destinations.AccessToken]
        });

        return new ClaimsPrincipal(identity);
    }
}
