namespace QualifyAI.AIOrchestration.Domain.Agents;
public interface IAgentRepository
{
    Task<Agent?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Agent entity, CancellationToken ct = default);
}
