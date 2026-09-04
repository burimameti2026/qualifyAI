namespace QualifyAI.AIOrchestration.Application.Abstractions.Persistence;

public interface IAIOrchestrationUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
