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

        db.BillingEvents.Add(new BillingEventRecord
        {
            Provider = provider,
            ExternalEventId = item.EventId,
            Type = item.Type,
            TenantId = item.TenantId,
            Status = item.Status,
            DataJson = item.Data is null ? null : JsonSerializer.Serialize(item.Data),
            OccurredAtUtc = item.OccurredAtUtc
        });

        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            if (await db.BillingEvents.AnyAsync(x => x.Provider == provider && x.ExternalEventId == item.EventId, ct)) return false;
            throw;
        }

        events.Record(new(item.TenantId,"billing",item.Status,$"Billing event {item.Type}",item.OccurredAtUtc,item.Data,$"{provider}:{item.EventId}",provider));
        if (item.Status is "cancelled" or "expired" or "past_due") await licenses.ReconcileAsync(item.TenantId,ct);
        return true;
    }
}

public sealed class BillingProviderRegistry(IEnumerable<IBillingProvider> providers)
{
    public IBillingProvider Get(string provider) => providers.First(x => string.Equals(x.Name,provider,StringComparison.OrdinalIgnoreCase));
}
