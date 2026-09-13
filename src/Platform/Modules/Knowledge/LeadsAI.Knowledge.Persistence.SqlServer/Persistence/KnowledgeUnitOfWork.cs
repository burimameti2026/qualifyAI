using LeadsAI.Knowledge.Application.Abstractions.Persistence;

namespace LeadsAI.Knowledge.Persistence.SqlServer;

public sealed class KnowledgeUnitOfWork(KnowledgeDbContext dbContext) : IKnowledgeUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
