namespace QualifyAI.AIOrchestration.Application.Runtime;
public sealed record ToolExecutionResult(string ToolName, bool Success, string Result);
