namespace QualifyAI.AIOrchestration.Application.Runtime;
public sealed record AgentRunResult(string Reply, IReadOnlyList<ToolExecutionResult> Tools, decimal EstimatedCost);
