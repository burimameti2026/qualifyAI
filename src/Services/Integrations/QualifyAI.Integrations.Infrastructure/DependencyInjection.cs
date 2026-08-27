using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Integrations.Application.Abstractions.Persistence;
using QualifyAI.Integrations.Domain.Integrations;
using QualifyAI.Integrations.Infrastructure.Persistence;
using QualifyAI.Integrations.Infrastructure.Persistence.Repositories;

namespace QualifyAI.Integrations.Infrastructure;

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
