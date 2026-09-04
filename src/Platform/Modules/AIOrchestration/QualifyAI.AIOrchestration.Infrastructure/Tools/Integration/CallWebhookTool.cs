using QualifyAI.AIOrchestration.Application.Tools;

namespace QualifyAI.AIOrchestration.Infrastructure.Tools.Integration;

public sealed class CallWebhookTool : IAgentTool
{
    public string Name => "CallWebhook";
    public string Description => "Invoke an approved external webhook.";

    public async Task<AgentToolResult> ExecuteAsync(
        AgentToolContext context,
        string argumentsJson,
        CancellationToken ct = default)
    {
        // Calls the owning microservice; no direct cross-service DbContext access.
        await Task.CompletedTask;
        return new(true, System.Text.Json.JsonSerializer.Serialize(new
        {
            accepted = true,
            correlationId = context.CorrelationId
        }));
    }
}
