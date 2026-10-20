using Microsoft.EntityFrameworkCore;
using LeadsAI.AIOrchestration.Domain.Agents;

namespace LeadsAI.AIOrchestration.Persistence.SqlServer.Repositories;

public sealed class AgentRepository(AIOrchestrationDbContext db) : IAgentRepository
{
    public Task<Agent?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.Agents.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public Task AddAsync(Agent entity, CancellationToken ct = default)
    {
        db.Agents.Add(entity);
        return Task.CompletedTask;
    }
}
