using QualifyAI.Domain;

namespace QualifyAI.Application.Abstractions.Persistence;

public interface IKnowledgeAiRepository
{
    Task<IReadOnlyList<KnowledgeBase>> ListKnowledgeBasesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeDocument>> ListKnowledgeDocumentsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<KnowledgeDocument?> GetKnowledgeDocumentAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeChunk>> ListKnowledgeChunksAsync(Guid tenantId, Guid documentId, CancellationToken cancellationToken = default);
    void AddKnowledgeDocument(KnowledgeDocument document);
    void RemoveKnowledgeChunks(IEnumerable<KnowledgeChunk> chunks);
    void AddKnowledgeChunks(IEnumerable<KnowledgeChunk> chunks);

    Task<IReadOnlyList<KnowledgeGap>> ListKnowledgeGapsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<KnowledgeGap?> GetKnowledgeGapAsync(Guid tenantId, Guid gapId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiAgent>> ListAiAgentsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<AiAgent?> GetAiAgentAsync(Guid tenantId, Guid agentId, CancellationToken cancellationToken = default);
    Task<bool> KnowledgeBaseExistsAsync(Guid tenantId, Guid knowledgeBaseId, CancellationToken cancellationToken = default);
    void AddAiAgent(AiAgent agent);
}
