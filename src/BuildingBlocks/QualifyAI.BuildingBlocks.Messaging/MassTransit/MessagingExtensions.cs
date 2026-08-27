using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace QualifyAI.BuildingBlocks.Messaging.MassTransit;

public static class MessagingExtensions
{
    public static IServiceCollection AddQualifyAiMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        services.AddMassTransit(x =>
        {
            configure?.Invoke(x);
            x.SetKebabCaseEndpointNameFormatter();

            x.UsingRabbitMq((context, cfg) =>
            {
                var host = configuration["RabbitMq:Host"] ?? "localhost";
                var user = configuration["RabbitMq:Username"] ?? "guest";
                var pass = configuration["RabbitMq:Password"] ?? "guest";

                cfg.Host(host, "/", h =>
                {
                    h.Username(user);
                    h.Password(pass);
                });

                cfg.UseMessageRetry(retry => retry.Intervals(
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(3),
                    TimeSpan.FromSeconds(10),
                    TimeSpan.FromSeconds(30)));

                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }
}
