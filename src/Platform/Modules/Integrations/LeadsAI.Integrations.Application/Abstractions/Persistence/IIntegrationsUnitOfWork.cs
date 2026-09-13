namespace LeadsAI.Integrations.Application.Abstractions.Persistence;

public interface IIntegrationsUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
