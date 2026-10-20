using LeadsAI.BuildingBlocks.Application.CQRS;
namespace LeadsAI.Notifications.Application.Notifications.Commands.Create;
public sealed record CreateNotificationCommand(Guid TenantId, string Name) : ICommand<Guid>;
