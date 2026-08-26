using QualifyAI.Notifications.Application.Abstractions.Persistence;

namespace QualifyAI.Notifications.Infrastructure.Persistence;

public sealed class NotificationsUnitOfWork(NotificationsDbContext dbContext) : INotificationsUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => dbContext.SaveChangesAsync(cancellationToken);
}
