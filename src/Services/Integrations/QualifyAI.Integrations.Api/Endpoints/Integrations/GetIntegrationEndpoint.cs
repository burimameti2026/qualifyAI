using MediatR;
using QualifyAI.Integrations.Application.Integrations.Queries.GetById;
namespace QualifyAI.Integrations.Api.Endpoints.Integrations;
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
