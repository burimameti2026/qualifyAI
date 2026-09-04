using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.AIOrchestration.Application.Abstractions.Persistence;
using QualifyAI.AIOrchestration.Domain.Agents;
using QualifyAI.AIOrchestration.Persistence.SqlServer;
using QualifyAI.AIOrchestration.Persistence.SqlServer.Repositories;

namespace QualifyAI.AIOrchestration.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAIOrchestrationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AIOrchestrationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("AIOrchestrationDb"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<IAIOrchestrationUnitOfWork, AIOrchestrationUnitOfWork>();
        return services;
    }
}
