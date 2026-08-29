using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Application;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Infrastructure.Messaging.Consumers;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Infrastructure.Automation;
using QualifyAI.Persistence.SqlServer;
using QualifyAI.Persistence.SqlServer.Repositories;

namespace QualifyAI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        bool allowDevelopmentModelDrift = false)
    {
        services.AddDbContext<AppDbContext>(options =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                sql => sql.EnableRetryOnFailure());

            // Local development must remain startable while model changes are being
            // captured in explicit migrations. Production keeps EF's strict guard.
            if (allowDevelopmentModelDrift)
                options.ConfigureWarnings(warnings =>
                    warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IBusinessUnitOfWork, BusinessUnitOfWork>();
        services.AddScoped<ICrmRepository, CrmRepository>();
        services.AddScoped<ISupportRepository, SupportRepository>();
        services.AddScoped<IKnowledgeAiRepository, KnowledgeAiRepository>();
        services.AddScoped<IWorkflowAutomationRepository, WorkflowAutomationRepository>();
        services.AddScoped<ITenantEntitlementRepository, TenantEntitlementRepository>();
        services.AddScoped<IdentityEntitlementInboxProcessor>();

        services.AddScoped<ITenantContext, TenantContext>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IKnowledgeRetriever, SqlKnowledgeRetriever>();
        services.AddScoped<IAiProvider, LocalAiProvider>();
        services.AddScoped<IAiTool, CreateLeadTool>();
        services.AddScoped<IAiTool, CreateTicketTool>();
        services.AddScoped<IAiTool, SearchKnowledgeTool>();
        services.AddScoped<IAiToolRegistry, AiToolRegistry>();
        services.AddScoped<IIntegrationRegistry, IntegrationRegistry>();
        services.AddScoped<SalesAutomationService>();
        services.AddScoped<DemoSeeder>();
        services.AddScoped<CampaignExecutionService>();
        services.AddScoped<AutomationActionExecutor>();

        return services;
    }
}
