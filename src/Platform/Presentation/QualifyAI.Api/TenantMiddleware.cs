using QualifyAI.Application;
using QualifyAI.Application.Abstractions.Persistence;

namespace QualifyAI.Api;

public sealed class TenantMiddleware(RequestDelegate next, IConfiguration configuration)
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

        var slug = (context.Request.Headers["X-Tenant"].FirstOrDefault()
            ?? context.Request.Query["tenant"].FirstOrDefault())?.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(slug))
        {
            // Bootstrap tenants have their database routed before a tenant DB can be
            // queried for entitlement metadata. The configured tenant id is only used
            // for that bootstrap resolution; TenantAccessMiddleware still verifies the
            // active license from the routed tenant database before serving the request.
            var configuredTenantId = configuration[$"TenantBootstrap:{slug}:TenantId"];
            if (Guid.TryParse(configuredTenantId, out var bootstrapTenantId))
            {
                tenantContext.Set(new(bootstrapTenantId, slug));
            }
            else
            {
                var entitlement = await entitlements.FindActiveBySlugAsync(slug, context.RequestAborted);
                if (entitlement is not null)
                    tenantContext.Set(new(entitlement.TenantId, entitlement.TenantSlug));
            }
        }

        await next(context);
    }
}
