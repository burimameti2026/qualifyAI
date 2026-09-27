using Microsoft.Extensions.DependencyInjection;
using LeadsAI.Infrastructure.Acquisition;

namespace LeadsAI.Infrastructure;

public static class JobQueueDependencyInjection
{
    public static IServiceCollection AddAgentJobQueue(this IServiceCollection services)
    {
        services.AddScoped<IAgentJobQueue, AgentJobQueue>();
        return services;
    }
}
