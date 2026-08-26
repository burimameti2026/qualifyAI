using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
namespace QualifyAI.Knowledge.Application;
public static class DependencyInjection
{
    public static IServiceCollection AddKnowledgeApplication(this IServiceCollection services)
    {
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
