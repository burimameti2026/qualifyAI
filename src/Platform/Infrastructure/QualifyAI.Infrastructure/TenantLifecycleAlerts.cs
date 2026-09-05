using System.Collections.Concurrent;

namespace QualifyAI.Infrastructure;

public sealed record TenantAlert(Guid Id, Guid TenantId, string Severity, string Type, string Message, DateTime CreatedAtUtc, bool Acknowledged = false);
public interface ITenantAlertService { void Raise(Guid tenantId, string severity, string type, string message); IReadOnlyList<TenantAlert> Get(Guid tenantId, int take = 100); void Acknowledge(Guid tenantId, Guid alertId); }

public sealed class TenantAlertService(ITenantLifecycleEventStore events) : ITenantAlertService
{
    private readonly ConcurrentDictionary<Guid, ConcurrentQueue<TenantAlert>> _alerts = new();
    public void Raise(Guid tenantId, string severity, string type, string message)
    {
        var alert = new TenantAlert(Guid.NewGuid(), tenantId, severity, type, message, DateTime.UtcNow);
        _alerts.GetOrAdd(tenantId, _ => new()).Enqueue(alert);
        events.Record(new(tenantId, "alert", severity, message, alert.CreatedAtUtc, new Dictionary<string,string>{{"type",type}}));
    }
    public IReadOnlyList<TenantAlert> Get(Guid tenantId, int take = 100) => _alerts.TryGetValue(tenantId, out var queue) ? queue.OrderByDescending(x => x.CreatedAtUtc).Take(Math.Clamp(take, 1, 500)).ToArray() : Array.Empty<TenantAlert>();
    public void Acknowledge(Guid tenantId, Guid alertId)
    {
        if (!_alerts.TryGetValue(tenantId, out var queue)) return;
        var all = queue.ToArray(); while (queue.TryDequeue(out _)) { }
        foreach (var alert in all) queue.Enqueue(alert.Id == alertId ? alert with { Acknowledged = true } : alert);
    }
}
