namespace QualifyAI.Knowledge.Infrastructure.Mongo;
public interface IKnowledgeChunkStore
{
    Task UpsertAsync(KnowledgeChunkDocument document, CancellationToken ct = default);
    Task<IReadOnlyList<KnowledgeChunkDocument>> GetBySourceAsync(
        Guid tenantId, Guid sourceId, CancellationToken ct = default);
    Task DeleteSourceAsync(Guid tenantId, Guid sourceId, CancellationToken ct = default);
}
