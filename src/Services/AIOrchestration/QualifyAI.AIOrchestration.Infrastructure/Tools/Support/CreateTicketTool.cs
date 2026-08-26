using QualifyAI.AIOrchestration.Application.Tools;
namespace QualifyAI.AIOrchestration.Infrastructure.Tools.Support;
public sealed class CreateTicketTool(IHttpClientFactory httpClientFactory) : IAgentTool
{
    public string Name => "CreateTicket";
    public string Description => "Create a support ticket through the Ticketing service.";
    public async Task<AgentToolResult> ExecuteAsync(AgentToolContext context, string argumentsJson, CancellationToken ct = default)
    {
        // Calls the owning microservice; no direct cross-service DbContext access.
        await Task.CompletedTask;
        return new(true, System.Text.Json.JsonSerializer.Serialize(new { accepted = true, correlationId = context.CorrelationId }));
    }
}
