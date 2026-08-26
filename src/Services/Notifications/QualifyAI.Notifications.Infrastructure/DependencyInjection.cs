using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Notifications.Domain.Notifications;
using QualifyAI.Notifications.Infrastructure.Persistence;
using QualifyAI.Notifications.Infrastructure.Persistence.Repositories;
namespace QualifyAI.Notifications.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddNotificationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<NotificationsDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("NotificationsDb")));
        services.AddScoped<INotificationRepository,NotificationRepository>();
        return services;
    }
}
