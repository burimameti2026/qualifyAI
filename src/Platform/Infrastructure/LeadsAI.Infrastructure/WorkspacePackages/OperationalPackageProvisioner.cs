using LeadsAI.AIOrchestration.Domain.Agents;
using LeadsAI.AIOrchestration.Persistence.SqlServer;
using LeadsAI.Automation.Domain.AutomationDefinitions;
using LeadsAI.Automation.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.WorkspacePackages;

public sealed class OperationalPackageProvisioner(
    AIOrchestrationDbContext aiDb,
    AutomationDbContext automationDb,
    AppDbContext db)
{
    public async Task ProvisionAsync(
        Guid tenantId,
        WorkspacePackageDefinition package,
        CancellationToken ct = default)
    {
        if (!string.Equals(package.ProvisioningMode, "operational", StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var agentName in package.Agents ?? Array.Empty<string>())
        {
            var existing = await aiDb.Agents
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Name == agentName, ct);

            if (existing is null)
                aiDb.Agents.Add(Agent.Create(tenantId, agentName));
        }

        foreach (var workflowName in package.Workflows ?? Array.Empty<string>())
        {
            var existing = await automationDb.AutomationDefinitions
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Name == workflowName, ct);

            if (existing is null)
                automationDb.AutomationDefinitions.Add(
                    AutomationDefinition.Create(tenantId, workflowName));
        }

        await ProvisionWorkflowDefinitionsAsync(tenantId, package, ct);

        await aiDb.SaveChangesAsync(ct);
        await automationDb.SaveChangesAsync(ct);
        await db.SaveChangesAsync(ct);
    }
}