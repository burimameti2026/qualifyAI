using QualifyAI.Application;
using QualifyAI.Application.Abstractions.Persistence;

namespace QualifyAI.Api;

public sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantEntitlementRepository entitlements)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var tenantIdValue = context.User.FindFirst("tenant_id")?.Value;
            var tenantSlug = context.User.FindFirst("tenant_slug")?.Value;

            if (Guid.TryParse(tenantIdValue, out var tenantId) && !string.IsNullOrWhiteSpace(tenantSlug))
                tenantContext.Set(new(tenantId, tenantSlug));

            await next(context);
            return;
        }

        var slug = context.Request.Headers["X-Tenant"].FirstOrDefault()
            ?? context.Request.Query["tenant"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(slug))
        {
            var entitlement = await entitlements.FindActiveBySlugAsync(
                slug.Trim().ToLowerInvariant(),
                context.RequestAborted);

            if (entitlement is not null)
                tenantContext.Set(new(entitlement.TenantId, entitlement.TenantSlug));
        }

        await next(context);
    }
}
