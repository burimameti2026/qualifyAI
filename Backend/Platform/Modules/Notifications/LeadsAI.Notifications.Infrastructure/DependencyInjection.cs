using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LeadsAI.Notifications.Application.Abstractions.Persistence;
using LeadsAI.Notifications.Domain.Notifications;
using LeadsAI.Notifications.Persistence.SqlServer;
using LeadsAI.Notifications.Persistence.SqlServer.Repositories;

namespace LeadsAI.Notifications.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("NotificationsDb"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<INotificationsUnitOfWork, NotificationsUnitOfWork>();
        return services;
    }
}
