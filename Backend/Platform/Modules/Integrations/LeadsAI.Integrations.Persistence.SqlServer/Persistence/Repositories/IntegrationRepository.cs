using Microsoft.EntityFrameworkCore;
using LeadsAI.Integrations.Domain.Integrations;

namespace LeadsAI.Integrations.Persistence.SqlServer.Repositories;

public sealed class IntegrationRepository(IntegrationsDbContext db) : IIntegrationRepository
{
    public Task<Integration?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.Integrations.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public Task AddAsync(Integration entity, CancellationToken ct = default)
    {
        db.Integrations.Add(entity);
        return Task.CompletedTask;
    }
}
