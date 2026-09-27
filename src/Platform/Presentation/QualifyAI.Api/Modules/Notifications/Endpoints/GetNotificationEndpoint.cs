using MediatR;
using QualifyAI.Notifications.Application.Notifications.Queries.GetById;
namespace QualifyAI.Api.Modules.Notifications.Endpoints;
public static class GetNotificationEndpoint
{
    public static IEndpointRouteBuilder MapGetNotification(this IEndpointRouteBuilder app)
    {
        app.MapGet("/notifications/{id:guid}", async (Guid id, Guid tenantId, ISender sender, CancellationToken ct) =>
        {
            var dto = await sender.Send(new GetNotificationByIdQuery(tenantId,id), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });
        return app;
    }
}
