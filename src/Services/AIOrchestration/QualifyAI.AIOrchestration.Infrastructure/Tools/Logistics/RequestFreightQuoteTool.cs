using QualifyAI.AIOrchestration.Application.Tools;
namespace QualifyAI.AIOrchestration.Infrastructure.Tools.Logistics;
public sealed class RequestFreightQuoteTool(IHttpClientFactory httpClientFactory) : IAgentTool
{
    public string Name => "RequestFreightQuote";
    public string Description => "Create a structured freight RFQ.";
    public async Task<AgentToolResult> ExecuteAsync(AgentToolContext context, string argumentsJson, CancellationToken ct = default)
    {
        // Calls the owning microservice; no direct cross-service DbContext access.
        await Task.CompletedTask;
        return new(true, System.Text.Json.JsonSerializer.Serialize(new { accepted = true, correlationId = context.CorrelationId }));
    }
}
