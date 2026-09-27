using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using QualifyAI.Persistence.SqlServer.Projections;

namespace QualifyAI.Infrastructure;

public sealed record TenantLifecycleEvent(Guid TenantId, string Type, string Status, string Message, DateTime OccurredAtUtc, IReadOnlyDictionary<string,string>? Data = null, string? CorrelationId = null, string Source = "system", string? ActorId = null);

public interface ITenantLifecycleEventStore
{
    // Kept for source compatibility with existing sync call sites; internally fire-and-forgets
    // onto a background-safe async path. Prefer RecordAsync everywhere you can.
    void Record(TenantLifecycleEvent item);

    Task RecordAsync(TenantLifecycleEvent item, CancellationToken ct = default);

    Task<IReadOnlyList<TenantLifecycleEvent>> GetAsync(Guid tenantId, int take = 100, CancellationToken ct = default);
}

public sealed class TenantLifecycleEventStore(
    IServiceScopeFactory scopeFactory,
    ILogger<TenantLifecycleEventStore> logger) : ITenantLifecycleEventStore
{
    public void Record(TenantLifecycleEvent item)
    {
        // Only safe for genuinely sync call sites (e.g. non-async legacy code).
        // Prefer RecordAsync from any async method — do not block on this from async code.
        RecordAsync(item).GetAwaiter().GetResult();
    }

    public async Task RecordAsync(TenantLifecycleEvent item, CancellationToken ct = default)
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
            await db.SaveChangesAsync(ct);
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
        => GetAsync(tenantId, take).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<TenantLifecycleEvent>> GetAsync(Guid tenantId, int take = 100, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var rows = await db.Set<TenantLifecycleEventRecord>()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(Math.Clamp(take, 1, 500))
            .ToArrayAsync(ct);

        return rows
            .Select(x => new TenantLifecycleEvent(
                x.TenantId,
                x.Type,
                x.Status,
                x.Message,
                x.OccurredAtUtc,
                x.DataJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(x.DataJson),
                x.CorrelationId,
                x.Source,
                x.ActorId))
            .ToArray();
    }
}