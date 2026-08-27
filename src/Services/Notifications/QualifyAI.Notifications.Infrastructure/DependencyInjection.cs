using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Notifications.Application.Abstractions.Persistence;
using QualifyAI.Notifications.Infrastructure.Persistence;
using QualifyAI.Notifications.Infrastructure.Persistence.Repositories;

namespace QualifyAI.Notifications.Infrastructure;

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
