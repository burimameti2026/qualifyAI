namespace QualifyAI.AIOrchestration.Application.Runtime;
public interface IAgentRuntime
{
    Task<AgentRunResult> RunAsync(AgentRunRequest request, CancellationToken ct = default);
}
