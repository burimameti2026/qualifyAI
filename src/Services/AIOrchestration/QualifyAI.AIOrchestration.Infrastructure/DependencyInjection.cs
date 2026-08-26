using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.AIOrchestration.Domain.Agents;
using QualifyAI.AIOrchestration.Infrastructure.Persistence;
using QualifyAI.AIOrchestration.Infrastructure.Persistence.Repositories;
namespace QualifyAI.AIOrchestration.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddAIOrchestrationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AIOrchestrationDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("AIOrchestrationDb")));
        services.AddScoped<IAgentRepository,AgentRepository>();
        return services;
    }
}
