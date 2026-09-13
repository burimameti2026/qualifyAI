using LeadsAI.AIOrchestration.Application.Abstractions.Persistence;

namespace LeadsAI.AIOrchestration.Persistence.SqlServer;

public sealed class AIOrchestrationUnitOfWork(AIOrchestrationDbContext dbContext) : IAIOrchestrationUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
