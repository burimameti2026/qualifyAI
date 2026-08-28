using QualifyAI.Identity.Application.Abstractions.Persistence;

namespace QualifyAI.Identity.Persistence.SqlServer;

public sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IIdentityUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
