using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public static class BillingQueryEndpoints
{
    public static IEndpointRouteBuilder MapBillingQueries(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/billing/tenants/{tenantId}", async (Guid tenantId, AppDbContext db, CancellationToken ct) =>
        {
            var subscription = await db.TenantBillingSubscriptions.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId, ct);
            var invoices = await db.TenantBillingInvoices.AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.UpdatedAtUtc).Take(100).ToListAsync(ct);
            var events = await db.BillingEvents.AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.OccurredAtUtc).Take(100).ToListAsync(ct);
            return Results.Ok(new { tenantId, subscription, invoices, events });
        });
        return endpoints;
    }
}
