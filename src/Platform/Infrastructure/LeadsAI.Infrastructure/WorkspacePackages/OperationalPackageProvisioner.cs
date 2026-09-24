using LeadsAI.AIOrchestration.Domain.Agents;
using LeadsAI.AIOrchestration.Persistence.SqlServer;
using LeadsAI.Automation.Domain.AutomationDefinitions;
using LeadsAI.Automation.Persistence.SqlServer;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;

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

    private async Task ProvisionWorkflowDefinitionsAsync(
        Guid tenantId,
        WorkspacePackageDefinition package,
        CancellationToken ct)
    {
        foreach (var workflowName in package.Workflows ?? Array.Empty<string>())
        {
            var flow = await db.QualificationFlows
                .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Name == workflowName, ct);

            if (flow is not null)
            {
                flow.Active = true;
                flow.UpdatedAtUtc = DateTime.UtcNow;
                continue;
            }

            flow = new QualificationFlow
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = workflowName.Trim(),
                Active = true,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            db.QualificationFlows.Add(flow);

            var nodes = BuildWorkflowNodes(tenantId, flow.Id, workflowName);
            db.WorkflowNodes.AddRange(nodes);
            db.WorkflowEdges.AddRange(BuildWorkflowEdges(tenantId, flow.Id, nodes));
        }
    }

    private static IReadOnlyList<WorkflowNode> BuildWorkflowNodes(
        Guid tenantId,
        Guid flowId,
        string workflowName)
    {
        var nodes = new List<WorkflowNode>
        {
            WorkflowNode.Create(
                tenantId, flowId, "discover", "discoverprospects",
                "{\"source\":\"package\"}", 0, 0),

            WorkflowNode.Create(
                tenantId, flowId, "enrich", "enrichcompany",
                "{\"required\":true}", 240, 0),

            WorkflowNode.Create(
                tenantId, flowId, "qualify", "qualify",
                "{\"threshold\":70}", 480, 0)
        };

        var normalized = workflowName.ToLowerInvariant();

        if (normalized.Contains("supplier"))
        {
            nodes.Add(WorkflowNode.Create(
                tenantId, flowId, "target", "addtotargetlist",
                "{\"listType\":\"supplier\"}", 720, 0));
        }
        else if (normalized.Contains("follow") || normalized.Contains("acquisition"))
        {
            nodes.Add(WorkflowNode.Create(
                tenantId, flowId, "outreach", "addtocampaign",
                "{\"approvalRequired\":true}", 720, 0));
        }
        else
        {
            nodes.Add(WorkflowNode.Create(
                tenantId, flowId, "target", "addtotargetlist",
                "{\"listType\":\"qualified\"}", 720, 0));
        }

        return nodes;
    }

    private static IReadOnlyList<WorkflowEdge> BuildWorkflowEdges(
        Guid tenantId,
        Guid flowId,
        IReadOnlyList<WorkflowNode> nodes)
        => nodes.Zip(
            nodes.Skip(1),
            (from, to) => WorkflowEdge.Create(
                tenantId, flowId, from.NodeKey, to.NodeKey, "{}")).ToArray();
}