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
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "tenant_not_provisioned", "The tenant is not provisioned for this service.");
            return;
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
