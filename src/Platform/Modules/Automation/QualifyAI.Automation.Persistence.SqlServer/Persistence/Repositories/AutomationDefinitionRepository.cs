using Microsoft.EntityFrameworkCore;
using QualifyAI.Automation.Domain.AutomationDefinitions;

namespace QualifyAI.Automation.Persistence.SqlServer.Repositories;

public sealed class AutomationDefinitionRepository(AutomationDbContext db) : IAutomationDefinitionRepository
{
    public Task<AutomationDefinition?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.AutomationDefinitions.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public Task AddAsync(AutomationDefinition entity, CancellationToken ct = default)
    {
        db.AutomationDefinitions.Add(entity);
        return Task.CompletedTask;
    }
}
