using QualifyAI.Knowledge.Application.Abstractions.Persistence;

namespace QualifyAI.Knowledge.Infrastructure.Persistence;

public sealed class KnowledgeUnitOfWork(KnowledgeDbContext dbContext) : IKnowledgeUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
