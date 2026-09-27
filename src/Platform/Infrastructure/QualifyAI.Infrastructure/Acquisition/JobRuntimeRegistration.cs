using Microsoft.Extensions.DependencyInjection;

namespace LeadsAI.Infrastructure.Acquisition;

public static class JobRuntimeRegistration
{
    public static IServiceCollection AddAgentJobRuntime(this IServiceCollection services)
    {
        services.AddScoped<IAgentJobFactory, AgentJobFactory>();
        services.AddScoped<IAgentJobExecutor, AutonomousAcquisitionJobExecutor>();
        services.AddHostedService<TenantJobWorkerPool>();
        return services;
    }
}
