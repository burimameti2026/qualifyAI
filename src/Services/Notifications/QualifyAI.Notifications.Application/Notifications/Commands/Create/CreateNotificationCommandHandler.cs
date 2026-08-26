using MediatR;
using QualifyAI.Notifications.Domain.Notifications;
namespace QualifyAI.Notifications.Application.Notifications.Commands.Create;
public sealed class CreateNotificationCommandHandler(INotificationRepository repository) : IRequestHandler<CreateNotificationCommand,Guid>
{
    public async Task<Guid> Handle(CreateNotificationCommand request, CancellationToken ct)
    {
        var entity = Notification.Create(request.TenantId, request.Name);
        await repository.AddAsync(entity, ct);
        return entity.Id;
    }
}
