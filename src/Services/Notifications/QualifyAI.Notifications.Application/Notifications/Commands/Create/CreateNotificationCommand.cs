using QualifyAI.BuildingBlocks.Application.CQRS;
namespace QualifyAI.Notifications.Application.Notifications.Commands.Create;
public sealed record CreateNotificationCommand(Guid TenantId, string Name) : ICommand<Guid>;
