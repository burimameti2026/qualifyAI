using MediatR;
using QualifyAI.AIOrchestration.Application.Agents.Queries.GetById;
namespace QualifyAI.Api.Modules.AIOrchestration.Endpoints;
public static class GetAgentEndpoint
{
    public static IEndpointRouteBuilder MapGetAgent(this IEndpointRouteBuilder app)
    {
        app.MapGet("/agents/{id:guid}", async (Guid id, Guid tenantId, ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetAgentByIdQuery(tenantId,id), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });
        return app;
    }
}
