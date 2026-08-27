using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Persistence.Repositories;

public sealed class WorkflowAutomationRepository(AppDbContext db) : IWorkflowAutomationRepository
{
    public async Task<IReadOnlyList<QualificationFlow>> ListWorkflowsAsync(Guid tenantId, CancellationToken ct = default)
        => await db.QualificationFlows.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).ToListAsync(ct);

    public Task<bool> WorkflowExistsAsync(Guid tenantId, Guid flowId, CancellationToken ct = default)
        => db.QualificationFlows.AnyAsync(x => x.TenantId == tenantId && x.Id == flowId, ct);

    public async Task<IReadOnlyList<WorkflowNode>> ListWorkflowNodesAsync(Guid tenantId, Guid flowId, bool tracked = false, CancellationToken ct = default)
    {
        var query = db.WorkflowNodes.Where(x => x.TenantId == tenantId && x.FlowId == flowId);
        return await (tracked ? query : query.AsNoTracking()).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowEdge>> ListWorkflowEdgesAsync(Guid tenantId, Guid flowId, bool tracked = false, CancellationToken ct = default)
    {
        var query = db.WorkflowEdges.Where(x => x.TenantId == tenantId && x.FlowId == flowId);
        return await (tracked ? query : query.AsNoTracking()).ToListAsync(ct);
    }

    public void ReplaceWorkflowDesigner(IEnumerable<WorkflowNode> oldNodes, IEnumerable<WorkflowEdge> oldEdges, IEnumerable<WorkflowNode> newNodes, IEnumerable<WorkflowEdge> newEdges)
    {
        db.WorkflowNodes.RemoveRange(oldNodes);
        db.WorkflowEdges.RemoveRange(oldEdges);
        db.WorkflowNodes.AddRange(newNodes);
        db.WorkflowEdges.AddRange(newEdges);
    }

    public async Task<IReadOnlyList<AutomationRule>> ListAutomationRulesAsync(Guid tenantId, CancellationToken ct = default)
        => await db.AutomationRules.AsNoTracking().Where(x => x.TenantId == tenantId).OrderBy(x => x.Name).ToListAsync(ct);

    public Task<AutomationRule?> GetAutomationRuleAsync(Guid tenantId, Guid ruleId, CancellationToken ct = default)
        => db.AutomationRules.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == ruleId, ct);

    public void AddAutomationRule(AutomationRule rule) => db.AutomationRules.Add(rule);
    public void AddAutomationRun(AutomationRun run) => db.AutomationRuns.Add(run);
}
