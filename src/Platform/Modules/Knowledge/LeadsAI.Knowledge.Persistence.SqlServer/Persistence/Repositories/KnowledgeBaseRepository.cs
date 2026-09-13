using Microsoft.EntityFrameworkCore;
using LeadsAI.Knowledge.Domain.KnowledgeBases;

namespace LeadsAI.Knowledge.Persistence.SqlServer.Repositories;

public sealed class KnowledgeBaseRepository(KnowledgeDbContext db) : IKnowledgeBaseRepository
{
    public Task<KnowledgeBase?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.KnowledgeBases.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public Task AddAsync(KnowledgeBase entity, CancellationToken ct = default)
    {
        db.KnowledgeBases.Add(entity);
        return Task.CompletedTask;
    }
}
