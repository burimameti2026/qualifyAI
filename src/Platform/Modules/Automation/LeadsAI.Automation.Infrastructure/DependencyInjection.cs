using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LeadsAI.Automation.Application.Abstractions.Persistence;
using LeadsAI.Automation.Domain.AutomationDefinitions;
using LeadsAI.Automation.Persistence.SqlServer;
using LeadsAI.Automation.Persistence.SqlServer.Repositories;

namespace LeadsAI.Automation.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAutomationInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AutomationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("AutomationDb"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IAutomationDefinitionRepository, AutomationDefinitionRepository>();
        services.AddScoped<IAutomationUnitOfWork, AutomationUnitOfWork>();
        return services;
    }
}
