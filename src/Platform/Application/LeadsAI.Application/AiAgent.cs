namespace LeadsAI.Application;

public sealed record AiAgentRequest(string Goal, string? ContextJson = null);
public sealed record AiAgentResult(string Message, string[] Suggestions, string? NextAction, string? Tool, string? ToolResult, string? ToolInput = null, bool RequiresApproval = false);

public interface IAiAgent
{
    Task<AiAgentResult> RunAsync(AiAgentRequest request, AiToolContext context, CancellationToken ct = default);
}
