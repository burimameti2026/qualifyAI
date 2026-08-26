using QualifyAI.Application;
using QualifyAI.Application.Abstractions.Persistence;

namespace QualifyAI.Api;

public sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ITenantContext tenantContext,
        ITenantProjectionRepository tenants)
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

        // Anonymous widget/public flows may resolve a workspace by slug. The Business
        // service reads its local tenant projection; Identity remains the authority.
        var slug = context.Request.Headers["X-Tenant"].FirstOrDefault()
            ?? context.Request.Query["tenant"].FirstOrDefault();

        if (!string.IsNullOrWhiteSpace(slug))
        {
            var tenant = await tenants.FindActiveBySlugAsync(
                slug.Trim().ToLowerInvariant(),
                context.RequestAborted);

            if (tenant is not null)
                tenantContext.Set(new(tenant.Id, tenant.Slug));
        }

        await next(context);
    }
}
