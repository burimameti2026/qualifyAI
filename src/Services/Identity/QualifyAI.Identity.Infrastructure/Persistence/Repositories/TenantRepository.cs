using Microsoft.EntityFrameworkCore;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Domain.Tenants;

namespace QualifyAI.Identity.Infrastructure.Persistence.Repositories;

public sealed class TenantRepository(IdentityDbContext dbContext) : ITenantRepository
{
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.Tenants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return dbContext.Tenants.FirstOrDefaultAsync(x => x.Slug == normalized, cancellationToken);
    }

    public Task<bool> SlugExistsAsync(string slug, CancellationToken cancellationToken = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        return dbContext.Tenants.AnyAsync(x => x.Slug == normalized, cancellationToken);
    }

    public Task AddAsync(Tenant tenant, CancellationToken cancellationToken = default)
        => dbContext.Tenants.AddAsync(tenant, cancellationToken).AsTask();
}
