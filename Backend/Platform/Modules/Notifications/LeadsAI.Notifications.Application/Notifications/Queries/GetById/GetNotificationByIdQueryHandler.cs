using MediatR;
using LeadsAI.Notifications.Domain.Notifications;
namespace LeadsAI.Notifications.Application.Notifications.Queries.GetById;
public sealed class GetNotificationByIdQueryHandler(INotificationRepository repository) : IRequestHandler<GetNotificationByIdQuery,NotificationDto?>
{
    public async Task<NotificationDto?> Handle(GetNotificationByIdQuery request, CancellationToken ct)
    {
        var entity = await repository.GetAsync(request.TenantId, request.Id, ct);
        return entity is null ? null : new(entity.Id, entity.TenantId, entity.Name, entity.CreatedAtUtc);
    }
}
