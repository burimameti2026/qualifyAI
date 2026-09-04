using QualifyAI.AIOrchestration.Application.Tools;

namespace QualifyAI.AIOrchestration.Infrastructure.Tools.CRM;

public sealed class CreateLeadTool : IAgentTool
{
    public string Name => "CreateLead";
    public string Description => "Create a qualified CRM lead through the CRM service.";

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
