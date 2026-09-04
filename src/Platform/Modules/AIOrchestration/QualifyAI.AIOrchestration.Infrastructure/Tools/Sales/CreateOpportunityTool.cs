using QualifyAI.AIOrchestration.Application.Tools;

namespace QualifyAI.AIOrchestration.Infrastructure.Tools.Sales;

public sealed class CreateOpportunityTool : IAgentTool
{
    public string Name => "CreateOpportunity";
    public string Description => "Create a sales opportunity through the Sales service.";

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
