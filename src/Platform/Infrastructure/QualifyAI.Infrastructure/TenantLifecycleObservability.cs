using System.Collections.Concurrent;

namespace QualifyAI.Infrastructure;

public sealed record TenantLifecycleEvent(Guid TenantId, string Type, string Status, string Message, DateTime OccurredAtUtc, IReadOnlyDictionary<string,string>? Data = null);
public interface ITenantLifecycleEventStore { void Record(TenantLifecycleEvent item); IReadOnlyList<TenantLifecycleEvent> Get(Guid tenantId, int take = 100); }
public sealed class TenantLifecycleEventStore : ITenantLifecycleEventStore
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<TenantLifecycleEvent>> _events = new();
    public void Record(TenantLifecycleEvent item)
    {
        var queue = _events.GetOrAdd(item.TenantId, _ => new ConcurrentQueue<TenantLifecycleEvent>());
        queue.Enqueue(item);
        while (queue.Count > 500 && queue.TryDequeue(out _)) { }
    }
    public IReadOnlyList<TenantLifecycleEvent> Get(Guid tenantId, int take = 100) => _events.TryGetValue(tenantId, out var queue) ? queue.OrderByDescending(x => x.OccurredAtUtc).Take(Math.Clamp(take, 1, 500)).ToArray() : Array.Empty<TenantLifecycleEvent>();
}
