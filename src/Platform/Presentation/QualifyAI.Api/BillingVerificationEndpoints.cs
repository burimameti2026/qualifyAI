using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public static class BillingVerificationEndpoints
{
    public static IEndpointRouteBuilder MapBillingVerification(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/billing/tenants/{tenantId}/verify", async (Guid tenantId, AppDbContext db, CancellationToken ct) =>
        {
            var subscription = await db.TenantBillingSubscriptions.AsNoTracking().AnyAsync(x => x.TenantId == tenantId, ct);
            var invoiceCount = await db.TenantBillingInvoices.AsNoTracking().CountAsync(x => x.TenantId == tenantId, ct);
            var eventCount = await db.BillingEvents.AsNoTracking().CountAsync(x => x.TenantId == tenantId, ct);
            return Results.Ok(new
            {
                tenantId,
                schema = new { billingEvents = true, subscriptions = true, invoices = true },
                data = new { subscription, invoiceCount, eventCount },
                ready = true
            });
        });
        return endpoints;
    }
}
