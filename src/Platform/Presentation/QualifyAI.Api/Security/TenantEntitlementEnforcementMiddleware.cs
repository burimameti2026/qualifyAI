using QualifyAI.Application;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.BuildingBlocks.Security.Claims;

namespace QualifyAI.Api.Security;

/// <summary>
/// Enforces the current tenant/license state from the local Identity projection.
/// JWT claims establish the caller identity, while the projection remains the
/// authoritative runtime entitlement snapshot for the Business service.
/// </summary>
public sealed class TenantEntitlementEnforcementMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantEntitlementRepository entitlements)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        var tenantValue = context.User.FindFirst(QualifyAiClaimTypes.TenantId)?.Value;
        if (!Guid.TryParse(tenantValue, out var tenantId))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "tenant_claim_missing", "The access token does not contain a valid tenant identifier.");
            return;
        }

        var entitlement = await entitlements.GetAsync(tenantId, context.RequestAborted);
        if (entitlement is null)
        {
            var slug = context.User.FindFirst(QualifyAiClaimTypes.TenantSlug)?.Value;
            var plan = context.User.FindFirst(QualifyAiClaimTypes.LicensePlan)?.Value;
            var status = context.User.FindFirst(QualifyAiClaimTypes.LicenseStatus)?.Value;
            var modules = context.User.FindAll(QualifyAiClaimTypes.Module).Select(x => x.Value).ToArray();
            var versionValue = context.User.FindFirst(QualifyAiClaimTypes.LicenseVersion)?.Value;
            var expiresValue = context.User.FindFirst("exp")?.Value;

            if (string.IsNullOrWhiteSpace(slug) || string.IsNullOrWhiteSpace(status) || modules.Length == 0)
            {
                await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "tenant_not_provisioned", "The tenant is not provisioned for this service.");
                return;
            }

            long.TryParse(versionValue, out var version);
            DateTime? tokenExpiresAtUtc = long.TryParse(expiresValue, out var expiresUnix)
                ? DateTimeOffset.FromUnixTimeSeconds(expiresUnix).UtcDateTime
                : null;

            entitlement = await entitlements.ProvisionFromSignedTokenAsync(
                tenantId,
                slug,
                plan ?? "unassigned",
                status,
                version,
                modules,
                tokenExpiresAtUtc,
                context.RequestAborted);
        }

        if (!entitlement.IsAccessibleAt(DateTime.UtcNow))
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "license_inactive", "The tenant or license is inactive, expired, suspended, or not yet effective.");
            return;
        }

        var tokenVersionValue = context.User.FindFirst(QualifyAiClaimTypes.LicenseVersion)?.Value;
        if (long.TryParse(tokenVersionValue, out var tokenVersion)
            && tokenVersion != entitlement.Version)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "entitlement_token_stale", "The license changed after this access token was issued. Refresh authentication and retry.");
            return;
        }

        // Downstream request handlers rely on ITenantContext. Populate it from the
        // authoritative entitlement projection even when the JWT has no tenant_slug claim.
        tenantContext.Set(new CurrentTenant(entitlement.TenantId, entitlement.TenantSlug));
        context.Items["TenantEntitlements"] = entitlement;
        await next(context);
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string code, string detail)
    {
        context.Response.StatusCode = statusCode;
        return context.Response.WriteAsJsonAsync(new
        {
            type = $"https://qualifyai.dev/problems/{code}",
            title = statusCode == StatusCodes.Status401Unauthorized ? "Authentication required" : "Access denied",
            status = statusCode,
            code,
            detail
        }, context.RequestAborted);
    }
}
