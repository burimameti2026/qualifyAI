using QualifyAI.Application.Abstractions.Persistence;

namespace QualifyAI.Infrastructure.Persistence;

public sealed class BusinessUnitOfWork(AppDbContext dbContext) : IBusinessUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
