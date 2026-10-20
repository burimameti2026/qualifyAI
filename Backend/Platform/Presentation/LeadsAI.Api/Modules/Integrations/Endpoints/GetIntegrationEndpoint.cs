using MediatR;
using LeadsAI.Integrations.Application.Integrations.Queries.GetById;
namespace LeadsAI.Api.Modules.Integrations.Endpoints;
public static class GetIntegrationEndpoint
{
    public static IEndpointRouteBuilder MapGetIntegration(this IEndpointRouteBuilder app)
    {
        app.MapGet("/integrations/{id:guid}", async (Guid id, Guid tenantId, ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetIntegrationByIdQuery(tenantId,id), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });
        return app;
    }
}
