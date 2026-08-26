using MediatR;
using QualifyAI.Notifications.Application.Notifications.Commands.Create;
namespace QualifyAI.Notifications.Api.Endpoints.Notifications;
public static class CreateNotificationEndpoint
{
    public static IEndpointRouteBuilder MapCreateNotification(this IEndpointRouteBuilder app)
    {
        app.MapPost("/notifications", async (CreateNotificationRequest request, ISender sender, CancellationToken ct) =>
        {
            var id = await sender.Send(new CreateNotificationCommand(request.TenantId, request.Name), ct);
            return Results.Created($"/notifications/{id}", new { id });
        });
        return app;
    }
}
public sealed record CreateNotificationRequest(Guid TenantId, string Name);
