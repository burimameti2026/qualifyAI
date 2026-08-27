using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Persistence.Repositories;

public sealed class KnowledgeAiRepository(AppDbContext db) : IKnowledgeAiRepository
{
    public async Task<IReadOnlyList<KnowledgeBase>> ListKnowledgeBasesAsync(Guid tenantId, CancellationToken ct = default)
        => await db.KnowledgeBases.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).ToListAsync(ct);

    public async Task<IReadOnlyList<KnowledgeDocument>> ListKnowledgeDocumentsAsync(Guid tenantId, CancellationToken ct = default)
        => await db.KnowledgeDocuments.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Title).ToListAsync(ct);

    public Task<KnowledgeDocument?> GetKnowledgeDocumentAsync(Guid tenantId, Guid documentId, CancellationToken ct = default)
        => db.KnowledgeDocuments.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == documentId, ct);

    public async Task<IReadOnlyList<KnowledgeChunk>> ListKnowledgeChunksAsync(Guid tenantId, Guid documentId, CancellationToken ct = default)
        => await db.KnowledgeChunks.Where(x => x.TenantId == tenantId && x.DocumentId == documentId).ToListAsync(ct);

    public void AddKnowledgeDocument(KnowledgeDocument document) => db.KnowledgeDocuments.Add(document);
    public void RemoveKnowledgeChunks(IEnumerable<KnowledgeChunk> chunks) => db.KnowledgeChunks.RemoveRange(chunks);
    public void AddKnowledgeChunks(IEnumerable<KnowledgeChunk> chunks) => db.KnowledgeChunks.AddRange(chunks);

    public async Task<IReadOnlyList<KnowledgeGap>> ListKnowledgeGapsAsync(Guid tenantId, CancellationToken ct = default)
        => await db.KnowledgeGaps.AsNoTracking().Where(x => x.TenantId == tenantId).OrderByDescending(x => x.ImpactScore).ToListAsync(ct);

    public Task<KnowledgeGap?> GetKnowledgeGapAsync(Guid tenantId, Guid gapId, CancellationToken ct = default)
        => db.KnowledgeGaps.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == gapId, ct);

    public async Task<IReadOnlyList<AiAgent>> ListAiAgentsAsync(Guid tenantId, CancellationToken ct = default)
        => await db.AiAgents.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).ToListAsync(ct);

    public Task<AiAgent?> GetAiAgentAsync(Guid tenantId, Guid agentId, CancellationToken ct = default)
        => db.AiAgents.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == agentId, ct);

    public Task<bool> KnowledgeBaseExistsAsync(Guid tenantId, Guid knowledgeBaseId, CancellationToken ct = default)
        => db.KnowledgeBases.AnyAsync(x => x.TenantId == tenantId && x.Id == knowledgeBaseId, ct);

    public void AddAiAgent(AiAgent agent) => db.AiAgents.Add(agent);
}
