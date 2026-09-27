namespace QualifyAI.Application.Abstractions.Persistence;

public interface IBusinessUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
