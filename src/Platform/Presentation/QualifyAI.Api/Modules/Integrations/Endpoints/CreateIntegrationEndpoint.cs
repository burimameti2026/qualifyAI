using MediatR;
using QualifyAI.Integrations.Application.Integrations.Commands.Create;
namespace QualifyAI.Api.Modules.Integrations.Endpoints;
public static class CreateIntegrationEndpoint
{
    public static IEndpointRouteBuilder MapCreateIntegration(this IEndpointRouteBuilder app)
    {
        app.MapPost("/integrations", async (CreateIntegrationRequest request, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateIntegrationCommand(request.TenantId, request.Name), ct);
            return Results.Created($"/api/modules/integrations/{id}", new { id });
        });
        return app;
    }
}
public sealed record CreateIntegrationRequest(Guid TenantId, string Name);
