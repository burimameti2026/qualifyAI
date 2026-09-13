

using LeadsAI.Identity.Application.Abstractions.Persistence;

namespace LeadsAI.Identity.Persistence.SqlServer;

public sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IIdentityUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
