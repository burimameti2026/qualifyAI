using Microsoft.Extensions.DependencyInjection;

namespace QualifyAI.Infrastructure.Acquisition;

public static class JobWorkerRegistration
{
    public static IServiceCollection AddTenantJobRuntime(this IServiceCollection services)
    {
        services.AddScoped<IAgentJobExecutor, AutonomousAcquisitionJobExecutor>();
        services.AddHostedService<TenantJobWorkerPool>();
        return services;
    }
}
