using QualifyAI.Identity.Application.Abstractions.Persistence;

namespace QualifyAI.Identity.Infrastructure.Persistence;

public sealed class IdentityUnitOfWork(IdentityDbContext dbContext) : IIdentityUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
