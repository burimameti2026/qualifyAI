using Microsoft.EntityFrameworkCore;
using LeadsAI.Notifications.Domain.Notifications;

namespace LeadsAI.Notifications.Persistence.SqlServer.Repositories;

public sealed class NotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public Task<Notification?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.Notifications.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

    public Task AddAsync(Notification entity, CancellationToken ct = default)
    {
        db.Notifications.Add(entity);
        return Task.CompletedTask;
    }
}
