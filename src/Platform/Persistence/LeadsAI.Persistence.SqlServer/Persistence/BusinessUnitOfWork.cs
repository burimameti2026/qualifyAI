using LeadsAI.Application.Abstractions.Persistence;

namespace LeadsAI.Persistence.SqlServer;

public sealed class BusinessUnitOfWork(AppDbContext dbContext) : IBusinessUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
