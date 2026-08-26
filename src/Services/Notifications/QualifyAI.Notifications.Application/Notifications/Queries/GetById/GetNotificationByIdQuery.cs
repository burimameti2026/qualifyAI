using QualifyAI.BuildingBlocks.Application.CQRS;
namespace QualifyAI.Notifications.Application.Notifications.Queries.GetById;
public sealed record GetNotificationByIdQuery(Guid TenantId, Guid Id) : IQuery<NotificationDto?>;
public sealed record NotificationDto(Guid Id, Guid TenantId, string Name, DateTime CreatedAtUtc);
