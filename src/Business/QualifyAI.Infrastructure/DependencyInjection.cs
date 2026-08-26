using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Application;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Infrastructure.Persistence;
using QualifyAI.Infrastructure.Persistence.Repositories;

namespace QualifyAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IBusinessUnitOfWork, BusinessUnitOfWork>();
        services.AddScoped<ICrmRepository, CrmRepository>();
        services.AddScoped<ISupportRepository, SupportRepository>();
        services.AddScoped<ITenantEntitlementRepository, TenantEntitlementRepository>();

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IKnowledgeRetriever, SqlKnowledgeRetriever>();
        services.AddScoped<IAiProvider, LocalAiProvider>();
        services.AddScoped<IAiTool, CreateLeadTool>();
        services.AddScoped<IAiTool, CreateTicketTool>();
        services.AddScoped<IAiTool, SearchKnowledgeTool>();
        services.AddScoped<IAiToolRegistry, AiToolRegistry>();
        services.AddScoped<IIntegrationRegistry, IntegrationRegistry>();
        services.AddScoped<SalesAutomationService>();
        services.AddScoped<DemoSeeder>();

        return services;
    }
}
