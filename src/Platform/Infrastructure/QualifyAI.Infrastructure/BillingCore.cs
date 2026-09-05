using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;
using QualifyAI.Persistence.SqlServer.Projections;

namespace QualifyAI.Infrastructure;

public sealed record BillingProviderEvent(string Provider,string EventId,string Type,Guid TenantId,string Status,DateTime OccurredAtUtc,IReadOnlyDictionary<string,string>? Data=null);
public sealed record BillingSubscription(Guid TenantId,string Provider,string ExternalSubscriptionId,string Plan,string Status,DateTime StartedAtUtc,DateTime? EndsAtUtc=null);
public interface IBillingProvider { string Name { get; } Task HandleAsync(BillingProviderEvent item,CancellationToken ct=default); }
public interface IBillingEventProcessor { Task<bool> ProcessAsync(BillingProviderEvent item,CancellationToken ct=default); }

public sealed class BillingEventProcessor(AppDbContext db, ITenantLifecycleEventStore events, ILicenseChangeOrchestrator licenses) : IBillingEventProcessor
{
    public async Task<bool> ProcessAsync(BillingProviderEvent item,CancellationToken ct=default)
    {
        var provider = item.Provider.Trim().ToLowerInvariant();
        var exists = await db.BillingEvents.AnyAsync(x => x.Provider == provider && x.ExternalEventId == item.EventId, ct);
        if (exists) return false;
        db.BillingEvents.Add(new BillingEventRecord { Provider=provider, ExternalEventId=item.EventId, Type=item.Type, TenantId=item.TenantId, Status=item.Status, DataJson=item.Data is null?null:JsonSerializer.Serialize(item.Data), OccurredAtUtc=item.OccurredAtUtc });
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException) { if (await db.BillingEvents.AnyAsync(x=>x.Provider==provider&&x.ExternalEventId==item.EventId,ct)) return false; throw; }

        var externalSubscriptionId = Get(item.Data,"subscriptionId") ?? Get(item.Data,"externalSubscriptionId");
        if (!string.IsNullOrWhiteSpace(externalSubscriptionId))
        {
            var subscription = await db.TenantBillingSubscriptions.SingleOrDefaultAsync(x => x.Provider == provider && x.ExternalSubscriptionId == externalSubscriptionId, ct)
                ?? await db.TenantBillingSubscriptions.SingleOrDefaultAsync(x => x.TenantId == item.TenantId, ct);
            var now = DateTime.UtcNow;
            if (subscription is null) { subscription = new TenantBillingSubscriptionRecord { TenantId=item.TenantId, Provider=provider, ExternalSubscriptionId=externalSubscriptionId, Plan=Get(item.Data,"plan") ?? "unknown", Status=item.Status, StartedAtUtc=ParseDate(Get(item.Data,"startedAtUtc")) ?? item.OccurredAtUtc }; db.TenantBillingSubscriptions.Add(subscription); }
            else { subscription.TenantId=item.TenantId; subscription.Provider=provider; subscription.ExternalSubscriptionId=externalSubscriptionId; subscription.Plan=Get(item.Data,"plan") ?? subscription.Plan; subscription.Status=item.Status; }
            subscription.CurrentPeriodEndsAtUtc=ParseDate(Get(item.Data,"currentPeriodEndsAtUtc")) ?? subscription.CurrentPeriodEndsAtUtc;
            if (item.Status is "cancelled" or "expired") subscription.CancelledAtUtc=item.OccurredAtUtc;
            subscription.UpdatedAtUtc=now;
            await db.SaveChangesAsync(ct);
        }

        var externalInvoiceId = Get(item.Data,"invoiceId") ?? Get(item.Data,"externalInvoiceId");
        if (!string.IsNullOrWhiteSpace(externalInvoiceId))
        {
            var invoice = await db.TenantBillingInvoices.SingleOrDefaultAsync(x => x.Provider == provider && x.ExternalInvoiceId == externalInvoiceId, ct);
            if (invoice is null) { invoice = new TenantBillingInvoiceRecord { TenantId=item.TenantId, Provider=provider, ExternalInvoiceId=externalInvoiceId, Status=item.Status }; db.TenantBillingInvoices.Add(invoice); }
            invoice.TenantId=item.TenantId; invoice.Status=item.Status; invoice.Currency=Get(item.Data,"currency") ?? invoice.Currency; invoice.AmountDue=ParseDecimal(Get(item.Data,"amountDue")) ?? invoice.AmountDue; invoice.AmountPaid=ParseDecimal(Get(item.Data,"amountPaid")) ?? invoice.AmountPaid; invoice.DueAtUtc=ParseDate(Get(item.Data,"dueAtUtc")) ?? invoice.DueAtUtc; invoice.PaidAtUtc=ParseDate(Get(item.Data,"paidAtUtc")) ?? invoice.PaidAtUtc; if (item.Status is "paid" && invoice.PaidAtUtc is null) invoice.PaidAtUtc=item.OccurredAtUtc; invoice.UpdatedAtUtc=DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }

        events.Record(new(item.TenantId,"billing",item.Status,$"Billing event {item.Type}",item.OccurredAtUtc,item.Data,$"{provider}:{item.EventId}",provider));
        if (item.Status is "cancelled" or "expired" or "past_due") await licenses.ReconcileAsync(item.TenantId,ct);
        return true;
    }
    private static string? Get(IReadOnlyDictionary<string,string>? data,string key) => data is not null && data.TryGetValue(key,out var value) ? value : null;
    private static DateTime? ParseDate(string? value) => DateTime.TryParse(value,out var parsed) ? parsed.ToUniversalTime() : null;
    private static decimal? ParseDecimal(string? value) => decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var parsed) ? parsed : null;
}

public sealed class BillingProviderRegistry(IEnumerable<IBillingProvider> providers)
{
    public IBillingProvider Get(string provider) => providers.First(x => string.Equals(x.Name,provider,StringComparison.OrdinalIgnoreCase));
}
