namespace QualifyAI.Knowledge.Application.Abstractions.Persistence;

public interface IKnowledgeUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
