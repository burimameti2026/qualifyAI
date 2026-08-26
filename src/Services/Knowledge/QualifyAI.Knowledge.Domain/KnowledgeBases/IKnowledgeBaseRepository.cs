namespace QualifyAI.Knowledge.Domain.KnowledgeBases;
public interface IKnowledgeBaseRepository
{
    Task<KnowledgeBase?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(KnowledgeBase entity, CancellationToken ct = default);
}
