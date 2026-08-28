namespace QualifyAI.Notifications.Domain.Notifications;
public interface INotificationRepository
{
    Task<Notification?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task AddAsync(Notification entity, CancellationToken ct = default);
}
