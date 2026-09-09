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
using QualifyAI.Infrastructure.WorkspacePackages;
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
        services.AddSingleton<ITenantDatabaseConnectionResolver, TenantDatabaseConnectionResolver>();
        services.AddScoped<ITenantContext, TenantContext>();

        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
        {
            var tenantContext = serviceProvider.GetRequiredService<ITenantContext>();
            var connectionResolver = serviceProvider.GetRequiredService<ITenantDatabaseConnectionResolver>();
            var connectionString = connectionResolver.ResolveConnectionString(tenantContext.Current);

            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure());
            if (allowDevelopmentModelDrift)
                options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        });

        services.AddSingleton<ITenantLifecycleEventStore,TenantLifecycleEventStore>();
        services.AddSingleton<ITenantAlertService,TenantAlertService>();
        services.AddScoped<ITenantLifecycleHealthService,TenantLifecycleHealthService>();
        services.AddSingleton(new BillingLifecyclePolicy());
        services.AddSingleton<IBillingLifecycleEngine,BillingLifecycleEngine>();
        services.AddScoped<IUsageMeter,PersistentUsageMeter>();
        services.AddScoped<IBillingQuotaEnforcer,BillingQuotaEnforcer>();
        services.AddScoped<IBillingAlertSink,PersistentBillingAlertSink>();
        services.AddScoped<IBillingEventProcessor,BillingEventProcessor>();
        services.AddScoped<IBillingProvider,StripeBillingProvider>();
        services.AddScoped<BillingProviderRegistry>();
        services.AddScoped<IBusinessUnitOfWork,BusinessUnitOfWork>();
        services.AddScoped<ICrmRepository,CrmRepository>();
        services.AddScoped<ISupportRepository,SupportRepository>();
        services.AddScoped<IKnowledgeAiRepository,KnowledgeAiRepository>();
        services.AddScoped<IWorkflowAutomationRepository,WorkflowAutomationRepository>();
        services.AddScoped<ITenantEntitlementRepository,TenantEntitlementRepository>();
        services.AddScoped<IdentityEntitlementInboxProcessor>();
        services.AddScoped<IGoldenPipelineProvisioner,GoldenPipelineProvisioner>();
        services.AddScoped<IModuleProvisioner,GoldenPipelineModuleProvisioner>();
        services.AddScoped<IModuleLifecycleHandler,GoldenPipelineModuleLifecycleHandler>();
        services.AddScoped<IModuleRegistry,ModuleRegistry>();
        services.AddScoped<IModuleProvisioningOrchestrator,ModuleProvisioningOrchestrator>();
        services.AddScoped<IModuleDeactivationOrchestrator,ModuleDeactivationOrchestrator>();
        services.AddScoped<ITenantLifecycleOrchestrator,TenantLifecycleOrchestrator>();
        services.AddScoped<ILicenseChangeOrchestrator,LicenseChangeOrchestrator>();
        services.AddHostedService<ModuleProvisioningRetryWorker>();
        services.AddHostedService<LicenseExpirationWorker>();
        services.AddHostedService<TenantLifecycleReconciliationWorker>();
        services.AddHostedService<AutonomousAcquisitionQueuedRunWorker>();
        services.AddHostedService<AutonomousAcquisitionSchedulerWorker>();
        services.AddScoped<IPasswordService,PasswordService>();
        services.AddScoped<IKnowledgeRetriever,SqlKnowledgeRetriever>();
        services.AddScoped<IAiProvider,LocalAiProvider>();
        services.AddScoped<IAiTool,CreateLeadTool>();
        services.AddScoped<IAiTool,CreateTicketTool>();
        services.AddScoped<IAiTool,SearchKnowledgeTool>();
        services.AddScoped<IAiToolRegistry,AiToolRegistry>();
        services.AddScoped<IIntegrationRegistry,IntegrationRegistry>();
        services.AddScoped<SalesAutomationService>();
        services.AddScoped<DemoSeeder>();
        services.AddScoped<CampaignExecutionService>();
        services.AddScoped<ProspectReplyProcessingService>();
        services.AddScoped<ProspectDiscoveryService>();
        services.AddSingleton<IAutonomousAcquisitionTemplateRegistry,AutonomousAcquisitionTemplateRegistry>();
        services.AddScoped<IAutonomousAcquisitionBackendService,AutonomousAcquisitionBackendService>();
        services.AddScoped<IAutonomousAcquisitionRunOrchestrator,AutonomousAcquisitionRunOrchestrator>();
        services.AddHttpClient<SerpApiProspectDiscoveryProvider>(c =>
        {
            c.BaseAddress = new Uri("https://serpapi.com/");
            c.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<IProspectDiscoveryProvider>(sp => sp.GetRequiredService<SerpApiProspectDiscoveryProvider>());
        services.AddScoped<AutomationActionExecutor>();
        services.AddScoped<RealisticScenarioService>();
        services.AddScoped<RealWorkspaceService>();
        services.AddScoped<IEmailDeliveryProvider,SmtpEmailProvider>();
        services.AddHttpClient<BrevoEmailProvider>(c =>
        {
            c.BaseAddress = new Uri("https://api.brevo.com/v3/");
            c.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddScoped<IEmailDeliveryProvider>(sp => sp.GetRequiredService<BrevoEmailProvider>());
        services.AddHttpClient<SendGridEmailProvider>();
        services.AddScoped<IEmailDeliveryProvider>(sp => sp.GetRequiredService<SendGridEmailProvider>());
        services.AddScoped<EmailDeliveryService>();

        return services;
    }
}
