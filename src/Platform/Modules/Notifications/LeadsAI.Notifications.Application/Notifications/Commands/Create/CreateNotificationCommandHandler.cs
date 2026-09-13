using MediatR;
using LeadsAI.Notifications.Application.Abstractions.Persistence;
using LeadsAI.Notifications.Domain.Notifications;

namespace LeadsAI.Notifications.Application.Notifications.Commands.Create;

public sealed class CreateNotificationCommandHandler(
    INotificationRepository repository,
    INotificationsUnitOfWork unitOfWork)
    : IRequestHandler<CreateNotificationCommand, Guid>
{
    public async Task<Guid> Handle(CreateNotificationCommand request, CancellationToken ct)
    {
        var entity = Notification.Create(request.TenantId, request.Name);
        await repository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return entity.Id;
    }
}
