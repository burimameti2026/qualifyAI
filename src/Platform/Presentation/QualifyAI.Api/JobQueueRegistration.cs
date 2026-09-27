using Microsoft.Extensions.DependencyInjection;
using LeadsAI.Infrastructure;

namespace LeadsAI.Api;

public static class JobQueueRegistration
{
    public static IServiceCollection AddJobQueue(this IServiceCollection services)
    {
        services.AddAgentJobQueue();
        return services;
    }
}
