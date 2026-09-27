using Microsoft.EntityFrameworkCore;
using QualifyAI.Application;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public sealed class TenantAccessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext, AppDbContext db)
    {
        var current = tenantContext.Current;
        if (current is not null && !IsExempt(context.Request.Path))
        {
            var entitlement = await db.TenantEntitlements.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == current.Id, context.RequestAborted);
            if (entitlement is null || !entitlement.LicenseStatus.Equals("active", StringComparison.OrdinalIgnoreCase) || !entitlement.TenantStatus.Equals("active", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { error = "tenant_access_suspended", licenseStatus = entitlement?.LicenseStatus, tenantStatus = entitlement?.TenantStatus }, context.RequestAborted);
                return;
            }
        }
        await next(context);
    }

    private static bool IsExempt(PathString path) => path.StartsWithSegments("/health") || path.StartsWithSegments("/swagger");
}
