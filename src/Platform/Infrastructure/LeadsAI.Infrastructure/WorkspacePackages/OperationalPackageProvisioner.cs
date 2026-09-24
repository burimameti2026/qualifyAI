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
        ProvisionAutomationRules(tenantId, package);

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
        var normalized = workflowName.ToLowerInvariant();

        static WorkflowNode Node(
            Guid tenantId,
            Guid flowId,
            string key,
            string type,
            string config,
            int x,
            int y = 0)
            => WorkflowNode.Create(tenantId, flowId, key, type, config, x, y);

        var nodes = new List<WorkflowNode>();

        if (normalized.Contains("supplier"))
        {
            nodes.Add(Node(tenantId, flowId, "discover", "discoverprospects",
                "{\"source\":\"serpapi\",\"minimumScore\":60}", 0));
            nodes.Add(Node(tenantId, flowId, "deduplicate", "deduplicate", "{}", 240));
            nodes.Add(Node(tenantId, flowId, "enrich", "enrichcompany",
                "{\"maximumResults\":100}", 480));
            nodes.Add(Node(tenantId, flowId, "qualify", "qualify",
                "{\"minimumScore\":70}", 720));
            nodes.Add(Node(tenantId, flowId, "target", "addtotargetlist",
                "{\"listType\":\"supplier\"}", 960));
        }
        else if (normalized.Contains("carrier"))
        {
            nodes.Add(Node(tenantId, flowId, "discover", "discoverprospects",
                "{\"source\":\"serpapi\",\"minimumScore\":60}", 0));
            nodes.Add(Node(tenantId, flowId, "deduplicate", "deduplicate", "{}", 240));
            nodes.Add(Node(tenantId, flowId, "enrich", "enrichcompany",
                "{\"maximumResults\":100}", 480));
            nodes.Add(Node(tenantId, flowId, "qualify", "qualify",
                "{\"minimumScore\":70}", 720));
            nodes.Add(Node(tenantId, flowId, "target", "addtotargetlist",
                "{\"listType\":\"carrier\"}", 960));
        }
        else if (normalized.Contains("follow"))
        {
            nodes.Add(Node(tenantId, flowId, "enrich", "enrichcompany",
                "{\"maximumResults\":100}", 0));
            nodes.Add(Node(tenantId, flowId, "qualify", "qualify",
                "{\"minimumScore\":70}", 240));
            nodes.Add(Node(tenantId, flowId, "personalize", "personalize",
                "{\"required\":true}", 480));
            nodes.Add(Node(tenantId, flowId, "approval", "requestapproval",
                "{\"approvalRequired\":true}", 720));
            nodes.Add(Node(tenantId, flowId, "campaign", "addtocampaign",
                "{\"approvalRequired\":true}", 960));
        }
        else if (normalized.Contains("acquisition"))
        {
            nodes.Add(Node(tenantId, flowId, "discover", "discoverprospects",
                "{\"source\":\"serpapi\",\"minimumScore\":60}", 0));
            nodes.Add(Node(tenantId, flowId, "deduplicate", "deduplicate", "{}", 240));
            nodes.Add(Node(tenantId, flowId, "enrich", "enrichcompany",
                "{\"maximumResults\":100}", 480));
            nodes.Add(Node(tenantId, flowId, "qualify", "qualify",
                "{\"minimumScore\":70}", 720));
            nodes.Add(Node(tenantId, flowId, "target", "addtotargetlist",
                "{\"listType\":\"qualified\"}", 960));
        }
        else
        {
            nodes.Add(Node(tenantId, flowId, "enrich", "enrichcompany",
                "{\"maximumResults\":100}", 0));
            nodes.Add(Node(tenantId, flowId, "qualify", "qualify",
                "{\"minimumScore\":70}", 240));
            nodes.Add(Node(tenantId, flowId, "target", "addtotargetlist",
                "{\"listType\":\"qualified\"}", 480));
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

    private void ProvisionAutomationRules(Guid tenantId, WorkspacePackageDefinition package)
    {
        foreach (var workflowName in package.Workflows ?? Array.Empty<string>())
        {
            var ruleName = $"package:{package.Id}:{workflowName}";
            if (db.AutomationRules.Any(x => x.TenantId == tenantId && x.Name == ruleName))
                continue;

            var normalized = workflowName.ToLowerInvariant();
            string actions;

            if (normalized.Contains("supplier") || normalized.Contains("carrier") ||
                normalized.Contains("acquisition") || normalized.Contains("qualification"))
            {
                actions = """
                [
                  {"type":"discover_prospects","source":"serpapi","minimumScore":60,"maximumResults":50,"createTargetList":false},
                  {"type":"deduplicate"},
                  {"type":"enrich_company","maximumResults":100},
                  {"type":"qualify","minimumScore":70},
                  {"type":"add_to_target_list"}
                ]
                """;
            }
            else if (normalized.Contains("follow"))
            {
                actions = """
                [
                  {"type":"enrich_company","maximumResults":100},
                  {"type":"qualify","minimumScore":70},
                  {"type":"personalize"},
                  {"type":"request_approval","title":"Review automated follow-up"}
                ]
                """;
            }
            else
            {
                actions = """
                [
                  {"type":"enrich_company","maximumResults":100},
                  {"type":"qualify","minimumScore":70},
                  {"type":"add_to_target_list"}
                ]
                """;
            }

            db.AutomationRules.Add(AutomationRule.Create(
                tenantId,
                ruleName,
                "schedule.weekday",
                "[]",
                actions,
                active: true));
        }
    }

}