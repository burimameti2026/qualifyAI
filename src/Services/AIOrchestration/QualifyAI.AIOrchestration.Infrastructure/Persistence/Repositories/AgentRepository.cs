using Microsoft.EntityFrameworkCore;
using QualifyAI.AIOrchestration.Domain.Agents;
namespace QualifyAI.AIOrchestration.Infrastructure.Persistence.Repositories;
public sealed class AgentRepository(AIOrchestrationDbContext db) : IAgentRepository
{
    public Task<Agent?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.Agents.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public async Task AddAsync(Agent entity, CancellationToken ct = default)
    {
        db.Agents.Add(entity);
        await db.SaveChangesAsync(ct);
    }
}
