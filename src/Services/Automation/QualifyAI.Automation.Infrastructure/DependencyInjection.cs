using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Automation.Domain.AutomationDefinitions;
using QualifyAI.Automation.Infrastructure.Persistence;
using QualifyAI.Automation.Infrastructure.Persistence.Repositories;
namespace QualifyAI.Automation.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddAutomationInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AutomationDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("AutomationDb")));
        services.AddScoped<IAutomationDefinitionRepository,AutomationDefinitionRepository>();
        return services;
    }
}
