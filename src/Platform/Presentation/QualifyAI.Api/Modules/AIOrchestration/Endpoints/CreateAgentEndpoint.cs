using MediatR;
using QualifyAI.AIOrchestration.Application.Agents.Commands.Create;
namespace QualifyAI.Api.Modules.AIOrchestration.Endpoints;
public static class CreateAgentEndpoint
{
    public static IEndpointRouteBuilder MapCreateAgent(this IEndpointRouteBuilder app)
    {
        app.MapPost("/agents", async (CreateAgentRequest request, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateAgentCommand(request.TenantId, request.Name), ct);
            return Results.Created($"/api/modules/ai/agents/{id}", new { id });
        });
        return app;
    }
}
public sealed record CreateAgentRequest(Guid TenantId, string Name);
