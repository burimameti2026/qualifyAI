using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Application.Entitlements;
using QualifyAI.Infrastructure.Persistence.Projections;

namespace QualifyAI.Infrastructure.Persistence.Repositories;

public sealed class TenantEntitlementRepository(AppDbContext dbContext) : ITenantEntitlementRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TenantEntitlementSnapshot?> GetAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.TenantEntitlements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<TenantEntitlementSnapshot?> FindActiveBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalizedSlug = slug.Trim().ToLowerInvariant();
        var now = DateTime.UtcNow;
        var entity = await dbContext.TenantEntitlements.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantSlug == normalizedSlug
                && x.TenantStatus == "active"
                && x.LicenseStatus == "active"
                && x.StartsAtUtc <= now
                && (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now), cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<Guid>> ListActiveTenantIdsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await dbContext.TenantEntitlements.AsNoTracking()
            .Where(x => x.TenantStatus == "active"
                && x.LicenseStatus == "active"
                && x.StartsAtUtc <= now
                && (!x.ExpiresAtUtc.HasValue || x.ExpiresAtUtc > now))
            .Select(x => x.TenantId)
            .ToListAsync(cancellationToken);
    }

    public async Task UpsertTenantAsync(Guid tenantId, string tenantSlug, string tenantStatus, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.TenantEntitlements.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (entity is null)
        {
            entity = new TenantEntitlementProjection { TenantId = tenantId };
            dbContext.TenantEntitlements.Add(entity);
        }

        entity.TenantSlug = tenantSlug.Trim().ToLowerInvariant();
        entity.TenantStatus = Normalize(tenantStatus, "pending");
        entity.UpdatedAtUtc = updatedAtUtc;
    }

    public async Task UpsertLicenseAsync(Guid tenantId, string plan, string licenseStatus, int maxUsers, DateTime startsAtUtc, DateTime? expiresAtUtc, long version, IReadOnlyCollection<string> modules, IReadOnlyDictionary<string, int>? limits, DateTime updatedAtUtc, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.TenantEntitlements.FirstOrDefaultAsync(x => x.TenantId == tenantId, cancellationToken);
        if (entity is null)
        {
            entity = new TenantEntitlementProjection { TenantId = tenantId };
            dbContext.TenantEntitlements.Add(entity);
        }

        if (entity.Version > version)
            return;

        entity.LicensePlan = Normalize(plan, "unassigned");
        entity.LicenseStatus = Normalize(licenseStatus, "unassigned");
        entity.MaxUsers = Math.Max(0, maxUsers);
        entity.StartsAtUtc = startsAtUtc;
        entity.ExpiresAtUtc = expiresAtUtc;
        entity.Version = version;
        entity.ModulesJson = JsonSerializer.Serialize(modules.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x), JsonOptions);
        entity.LimitsJson = JsonSerializer.Serialize(limits ?? new Dictionary<string, int> { ["users"] = Math.Max(0, maxUsers) }, JsonOptions);
        entity.UpdatedAtUtc = updatedAtUtc;
    }

    private static TenantEntitlementSnapshot Map(TenantEntitlementProjection entity)
    {
        var modules = JsonSerializer.Deserialize<string[]>(entity.ModulesJson, JsonOptions) ?? [];
        var limits = JsonSerializer.Deserialize<Dictionary<string, int>>(entity.LimitsJson, JsonOptions)
            ?? new Dictionary<string, int>();

        return new TenantEntitlementSnapshot(
            entity.TenantId,
            entity.TenantSlug,
            entity.TenantStatus,
            entity.LicensePlan,
            entity.LicenseStatus,
            entity.MaxUsers,
            entity.StartsAtUtc,
            entity.ExpiresAtUtc,
            entity.Version,
            modules,
            limits,
            entity.UpdatedAtUtc);
    }

    private static string Normalize(string value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim().ToLowerInvariant();
}
