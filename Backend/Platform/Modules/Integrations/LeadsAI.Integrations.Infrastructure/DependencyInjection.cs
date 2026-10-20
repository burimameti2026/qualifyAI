using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using LeadsAI.Integrations.Application.Abstractions.Persistence;
using LeadsAI.Integrations.Domain.Integrations;
using LeadsAI.Integrations.Persistence.SqlServer;
using LeadsAI.Integrations.Persistence.SqlServer.Repositories;

namespace LeadsAI.Integrations.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddIntegrationsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<IntegrationsDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("IntegrationsDb"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IIntegrationRepository, IntegrationRepository>();
        services.AddScoped<IIntegrationsUnitOfWork, IntegrationsUnitOfWork>();
        return services;
    }
}
