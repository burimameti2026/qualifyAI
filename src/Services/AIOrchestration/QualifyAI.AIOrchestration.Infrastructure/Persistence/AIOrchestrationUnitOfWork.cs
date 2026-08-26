using QualifyAI.AIOrchestration.Application.Abstractions.Persistence;

namespace QualifyAI.AIOrchestration.Infrastructure.Persistence;

public sealed class AIOrchestrationUnitOfWork(AIOrchestrationDbContext dbContext) : IAIOrchestrationUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
