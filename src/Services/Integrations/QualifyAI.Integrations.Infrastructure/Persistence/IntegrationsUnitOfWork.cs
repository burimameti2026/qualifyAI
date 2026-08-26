using QualifyAI.Integrations.Application.Abstractions.Persistence;

namespace QualifyAI.Integrations.Infrastructure.Persistence;

public sealed class IntegrationsUnitOfWork(IntegrationsDbContext dbContext) : IIntegrationsUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
