using LeadsAI.Integrations.Application.Abstractions.Persistence;

namespace LeadsAI.Integrations.Persistence.SqlServer;

public sealed class IntegrationsUnitOfWork(IntegrationsDbContext dbContext) : IIntegrationsUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
