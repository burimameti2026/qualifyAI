using Microsoft.EntityFrameworkCore;
using QualifyAI.Application;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api;

public sealed class TenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext ctx, AppDbContext db, ITenantContext tc)
    {
        string? slug = null;

        if (ctx.User.Identity?.IsAuthenticated == true)
        {
            // Regression gate: an authenticated caller cannot override tenant context
            // with X-Tenant/query/body input. Tenant comes only from the signed token.
            slug = ctx.User.FindFirst("tenant_slug")?.Value;
        }
        else
        {
            // Anonymous public/widget flows may resolve tenant by explicit workspace.
            slug = ctx.Request.Headers["X-Tenant"].FirstOrDefault()
                ?? ctx.Request.Query["tenant"].FirstOrDefault();
        }

        if (!string.IsNullOrWhiteSpace(slug))
        {
            var tenant = await db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Slug == slug && x.IsActive, ctx.RequestAborted);
            if (tenant is not null)
                tc.Set(new(tenant.Id, tenant.Slug));
        }

        await next(ctx);
    }
}
