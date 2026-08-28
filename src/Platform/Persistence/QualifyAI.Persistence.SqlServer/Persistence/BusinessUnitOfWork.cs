using QualifyAI.Application.Abstractions.Persistence;

namespace QualifyAI.Persistence.SqlServer;

public sealed class BusinessUnitOfWork(AppDbContext dbContext) : IBusinessUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
