using LeadsAI.Notifications.Application.Abstractions.Persistence;

namespace LeadsAI.Notifications.Persistence.SqlServer;

public sealed class NotificationsUnitOfWork(NotificationsDbContext dbContext) : INotificationsUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
