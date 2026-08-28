using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
namespace QualifyAI.AIOrchestration.Application;
public static class DependencyInjection
{
    public static IServiceCollection AddAIOrchestrationApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
