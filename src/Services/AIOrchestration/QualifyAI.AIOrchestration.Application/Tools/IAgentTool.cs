namespace QualifyAI.AIOrchestration.Application.Tools;
public interface IAgentTool
{
    string Name { get; }
    string Description { get; }
    Task<AgentToolResult> ExecuteAsync(AgentToolContext context, string argumentsJson, CancellationToken ct = default);
}
public sealed record AgentToolContext(Guid TenantId, Guid AgentId, Guid ConversationId, Guid CorrelationId);
public sealed record AgentToolResult(bool Success, string Json, string? Error = null);
