using MediatR;
using QualifyAI.AIOrchestration.Application.Agents.Commands.Create;
namespace QualifyAI.AIOrchestration.Api.Endpoints.Agents;
public static class CreateAgentEndpoint
{
    public static IEndpointRouteBuilder MapCreateAgent(this IEndpointRouteBuilder app)
    {
        app.MapPost("/agents", async (CreateAgentRequest request, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateAgentCommand(request.TenantId, request.Name), ct);
            return Results.Created($"/agents/{id}", new { id });
        });
        return app;
    }
}
public sealed record CreateAgentRequest(Guid TenantId, string Name);
