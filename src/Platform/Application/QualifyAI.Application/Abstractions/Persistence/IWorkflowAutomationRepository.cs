using QualifyAI.Domain;

namespace QualifyAI.Application.Abstractions.Persistence;

public interface IWorkflowAutomationRepository
{
    Task<IReadOnlyList<QualificationFlow>> ListWorkflowsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> WorkflowExistsAsync(Guid tenantId, Guid flowId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowNode>> ListWorkflowNodesAsync(Guid tenantId, Guid flowId, bool tracked = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkflowEdge>> ListWorkflowEdgesAsync(Guid tenantId, Guid flowId, bool tracked = false, CancellationToken cancellationToken = default);
    void ReplaceWorkflowDesigner(IEnumerable<WorkflowNode> oldNodes, IEnumerable<WorkflowEdge> oldEdges, IEnumerable<WorkflowNode> newNodes, IEnumerable<WorkflowEdge> newEdges);

    Task<IReadOnlyList<AutomationRule>> ListAutomationRulesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<AutomationRule?> GetAutomationRuleAsync(Guid tenantId, Guid ruleId, CancellationToken cancellationToken = default);
    void AddAutomationRule(AutomationRule rule);
    void AddAutomationRun(AutomationRun run);
}
