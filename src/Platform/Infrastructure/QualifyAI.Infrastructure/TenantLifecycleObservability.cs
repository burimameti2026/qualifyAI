using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using QualifyAI.Persistence.SqlServer.Projections;

namespace QualifyAI.Infrastructure;

public sealed record TenantLifecycleEvent(Guid TenantId, string Type, string Status, string Message, DateTime OccurredAtUtc, IReadOnlyDictionary<string,string>? Data = null, string? CorrelationId = null, string Source = "system", string? ActorId = null);
public interface ITenantLifecycleEventStore { void Record(TenantLifecycleEvent item); IReadOnlyList<TenantLifecycleEvent> Get(Guid tenantId, int take = 100); }

public sealed class TenantLifecycleEventStore(
    IServiceScopeFactory scopeFactory,
    ILogger<TenantLifecycleEventStore> logger) : ITenantLifecycleEventStore
{
    public void Record(TenantLifecycleEvent item)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Set<TenantLifecycleEventRecord>().Add(new TenantLifecycleEventRecord
            {
                TenantId = item.TenantId,
                Type = item.Type,
                Status = item.Status,
                Message = item.Message,
                DataJson = item.Data is null ? null : JsonSerializer.Serialize(item.Data),
                CorrelationId = item.CorrelationId,
                Source = item.Source,
                ActorId = item.ActorId,
                OccurredAtUtc = item.OccurredAtUtc,
                RecordedAtUtc = DateTime.UtcNow
            });
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // Observability must never take down reconciliation, billing, or API work.
            logger.LogError(
                ex,
                "Failed to persist tenant lifecycle event for {TenantId} ({Type}/{Status})",
                item.TenantId,
                item.Type,
                item.Status);
        }
    }

    public IReadOnlyList<TenantLifecycleEvent> Get(Guid tenantId, int take = 100)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return db.Set<TenantLifecycleEventRecord>().AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.OccurredAtUtc).Take(Math.Clamp(take, 1, 500)).AsEnumerable().Select(x => new TenantLifecycleEvent(x.TenantId, x.Type, x.Status, x.Message, x.OccurredAtUtc, x.DataJson is null ? null : JsonSerializer.Deserialize<Dictionary<string,string>>(x.DataJson), x.CorrelationId, x.Source, x.ActorId)).ToArray();
    }
}
