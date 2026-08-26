using Microsoft.EntityFrameworkCore;
using QualifyAI.Notifications.Domain.Notifications;
namespace QualifyAI.Notifications.Infrastructure.Persistence.Repositories;
public sealed class NotificationRepository(NotificationsDbContext db) : INotificationRepository
{
    public Task<Notification?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => db.Notifications.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
    public async Task AddAsync(Notification entity, CancellationToken ct = default)
    {
        db.Notifications.Add(entity);
        await db.SaveChangesAsync(ct);
    }
}
