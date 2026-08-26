namespace QualifyAI.Automation.Application.Orchestration;
public interface IAutomationOrchestrator
{
    Task<Guid> StartAsync(Guid tenantId, Guid definitionId, string triggerPayload, CancellationToken ct = default);
    Task ResumeAsync(Guid workflowInstanceId, CancellationToken ct = default);
}
