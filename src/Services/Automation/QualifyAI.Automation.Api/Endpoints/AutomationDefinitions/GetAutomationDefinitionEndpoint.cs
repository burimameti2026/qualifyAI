using MediatR;
using QualifyAI.Automation.Application.AutomationDefinitions.Queries.GetById;
namespace QualifyAI.Automation.Api.Endpoints.AutomationDefinitions;
public static class GetAutomationDefinitionEndpoint
{
    public static IEndpointRouteBuilder MapGetAutomationDefinition(this IEndpointRouteBuilder app)
    {
        app.MapGet("/automationdefinitions/{id:guid}", async (Guid id, Guid tenantId, ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetAutomationDefinitionByIdQuery(tenantId,id), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });
        return app;
    }
}
