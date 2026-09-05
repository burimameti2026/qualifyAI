namespace QualifyAI.Infrastructure;

public sealed record BillingProviderEvent(string Provider,string EventId,string Type,Guid TenantId,string Status,DateTime OccurredAtUtc,IReadOnlyDictionary<string,string>? Data=null);
public sealed record BillingSubscription(Guid TenantId,string Provider,string ExternalSubscriptionId,string Plan,string Status,DateTime StartedAtUtc,DateTime? EndsAtUtc=null);
public interface IBillingProvider { string Name { get; } Task HandleAsync(BillingProviderEvent item,CancellationToken ct=default); }
public interface IBillingEventProcessor { Task<bool> ProcessAsync(BillingProviderEvent item,CancellationToken ct=default); }

public sealed class BillingEventProcessor(ITenantLifecycleEventStore events, ILicenseChangeOrchestrator licenses) : IBillingEventProcessor
{
    private readonly HashSet<string> _processed = new(StringComparer.Ordinal);
    private readonly object _gate = new();
    public async Task<bool> ProcessAsync(BillingProviderEvent item,CancellationToken ct=default)
    {
        lock (_gate) { if (!_processed.Add($"{item.Provider}:{item.EventId}")) return false; }
        events.Record(new(item.TenantId,"billing",item.Status,$"Billing event {item.Type}",item.OccurredAtUtc,item.Data,$"{item.Provider}:{item.EventId}",item.Provider));
        if (item.Status is "cancelled" or "expired" or "past_due") await licenses.ReconcileAsync(item.TenantId,ct);
        return true;
    }
}

public sealed class BillingProviderRegistry(IEnumerable<IBillingProvider> providers)
{
    public IBillingProvider Get(string provider) => providers.First(x => string.Equals(x.Name,provider,StringComparison.OrdinalIgnoreCase));
}
