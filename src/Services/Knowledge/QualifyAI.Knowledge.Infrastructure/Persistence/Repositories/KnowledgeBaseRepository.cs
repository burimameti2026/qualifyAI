using Microsoft.EntityFrameworkCore;
using QualifyAI.Knowledge.Domain.KnowledgeBases;
namespace QualifyAI.Knowledge.Infrastructure.Persistence.Repositories;
public sealed class KnowledgeBaseRepository(KnowledgeDbContext db) : IKnowledgeBaseRepository
{
    public Task<KnowledgeBase?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.KnowledgeBases.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public async Task AddAsync(KnowledgeBase entity, CancellationToken ct = default)
    {
        db.KnowledgeBases.Add(entity);
        await db.SaveChangesAsync(ct);
    }
}
