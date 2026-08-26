namespace QualifyAI.Integrations.Domain.Integrations;
public interface IIntegrationRepository
{
    Task<Integration?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Integration entity, CancellationToken ct = default);
}
