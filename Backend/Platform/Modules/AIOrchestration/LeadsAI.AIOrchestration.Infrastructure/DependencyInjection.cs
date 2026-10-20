using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LeadsAI.AIOrchestration.Application.Abstractions.Persistence;
using LeadsAI.AIOrchestration.Domain.Agents;
using LeadsAI.AIOrchestration.Persistence.SqlServer;
using LeadsAI.AIOrchestration.Persistence.SqlServer.Repositories;

namespace LeadsAI.AIOrchestration.Infrastructure;

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
