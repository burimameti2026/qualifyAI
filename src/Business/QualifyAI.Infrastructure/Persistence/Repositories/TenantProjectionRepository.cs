using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Abstractions.Persistence;

namespace QualifyAI.Infrastructure.Persistence.Repositories;

public sealed class TenantProjectionRepository(AppDbContext dbContext) : ITenantProjectionRepository
{
    public Task<TenantProjection?> FindActiveBySlugAsync(string slug, CancellationToken cancellationToken = default)
        => dbContext.Tenants.AsNoTracking()
            .Where(x => x.Slug == slug && x.IsActive)
            .Select(x => new TenantProjection(x.Id, x.Slug, x.IsActive))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Guid>> ListActiveTenantIdsAsync(CancellationToken cancellationToken = default)
        => await dbContext.Tenants.AsNoTracking()
            .Where(x => x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);
}
