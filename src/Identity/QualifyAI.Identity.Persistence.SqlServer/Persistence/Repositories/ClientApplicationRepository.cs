using Microsoft.EntityFrameworkCore;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Domain.Clients;

namespace QualifyAI.Identity.Persistence.SqlServer.Repositories;

public sealed class ClientApplicationRepository(IdentityDbContext dbContext)
    : IClientApplicationRepository
{
    public Task<ClientApplication?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => dbContext.ClientApplications
            .Include(x => x.Scopes)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<ClientApplication?> GetByClientIdAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var normalized = clientId.Trim().ToLowerInvariant();
        return dbContext.ClientApplications
            .Include(x => x.Scopes)
            .FirstOrDefaultAsync(x => x.ClientId == normalized, cancellationToken);
    }

    public async Task<IReadOnlyList<ClientApplication>> ListAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ClientApplications
            .Include(x => x.Scopes)
            .AsQueryable();

        if (tenantId.HasValue)
            query = query.Where(x => x.TenantId == tenantId.Value);

        return await query
            .OrderBy(x => x.DisplayName)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> ClientIdExistsAsync(
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var normalized = clientId.Trim().ToLowerInvariant();
        return dbContext.ClientApplications.AnyAsync(x => x.ClientId == normalized, cancellationToken);
    }

    public Task AddAsync(
        ClientApplication clientApplication,
        CancellationToken cancellationToken = default)
        => dbContext.ClientApplications.AddAsync(clientApplication, cancellationToken).AsTask();
}
