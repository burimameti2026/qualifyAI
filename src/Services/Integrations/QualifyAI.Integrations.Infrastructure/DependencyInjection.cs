using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Integrations.Domain.Integrations;
using QualifyAI.Integrations.Infrastructure.Persistence;
using QualifyAI.Integrations.Infrastructure.Persistence.Repositories;
namespace QualifyAI.Integrations.Infrastructure;
public static class DependencyInjection
{
    public static IServiceCollection AddIntegrationsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<IntegrationsDbContext>(o => o.UseSqlServer(configuration.GetConnectionString("IntegrationsDb")));
        services.AddScoped<IIntegrationRepository,IntegrationRepository>();
        return services;
    }
}
