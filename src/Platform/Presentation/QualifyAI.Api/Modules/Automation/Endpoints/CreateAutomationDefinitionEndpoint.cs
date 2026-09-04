using MediatR;
using QualifyAI.Automation.Application.AutomationDefinitions.Commands.Create;
namespace QualifyAI.Api.Modules.Automation.Endpoints;
public static class CreateAutomationDefinitionEndpoint
{
    public static IEndpointRouteBuilder MapCreateAutomationDefinition(this IEndpointRouteBuilder app)
    {
        app.MapPost("/definitions", async (CreateAutomationDefinitionRequest request, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateAutomationDefinitionCommand(request.TenantId, request.Name), ct);
            return Results.Created($"/api/modules/automation/definitions/{id}", new { id });
        });
        return app;
    }
}
public sealed record CreateAutomationDefinitionRequest(Guid TenantId, string Name);
