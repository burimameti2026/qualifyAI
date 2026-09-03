using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QualifyAI.Application;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Infrastructure.Messaging.Consumers;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Infrastructure.Automation;
using QualifyAI.Infrastructure.Demo;
using QualifyAI.Infrastructure.Email;
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
        services.AddScoped<ProspectReplyProcessingService>();
        services.AddScoped<ProspectDiscoveryService>();
        services.AddHttpClient<SerpApiProspectDiscoveryProvider>(client =>
        {
            client.BaseAddress = new Uri("https://serpapi.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IProspectDiscoveryProvider>(sp => sp.GetRequiredService<SerpApiProspectDiscoveryProvider>());
        services.AddScoped<AutomationActionExecutor>();
        services.AddScoped<RealisticScenarioService>();
        services.AddScoped<IEmailDeliveryProvider, SmtpEmailProvider>();
        services.AddHttpClient<BrevoEmailProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.brevo.com/v3/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddScoped<IEmailDeliveryProvider>(sp => sp.GetRequiredService<BrevoEmailProvider>());
        services.AddHttpClient<SendGridEmailProvider>();
        services.AddScoped<IEmailDeliveryProvider>(sp => sp.GetRequiredService<SendGridEmailProvider>());
        services.AddScoped<EmailDeliveryService>();

        return services;
    }
}
