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
    public static IServiceCollection AddBusinessInfrastructure(this IServiceCollection services, IConfiguration configuration, bool allowDevelopmentModelDrift = false)
    {
        services.AddDbContext<AppDbContext>(options => { options.UseSqlServer(configuration.GetConnectionString("DefaultConnection"), sql => sql.EnableRetryOnFailure()); if (allowDevelopmentModelDrift) options.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)); });
        services.AddSingleton<ITenantLifecycleEventStore, TenantLifecycleEventStore>(); services.AddSingleton<ITenantAlertService, TenantAlertService>(); services.AddScoped<ITenantLifecycleHealthService, TenantLifecycleHealthService>(); services.AddSingleton(new BillingLifecyclePolicy()); services.AddSingleton<IBillingLifecycleEngine, BillingLifecycleEngine>(); services.AddSingleton<IUsageMeter, InMemoryUsageMeter>(); services.AddSingleton<IBillingAlertSink, NullBillingAlertSink>(); services.AddScoped<IBillingEventProcessor, BillingEventProcessor>(); services.AddScoped<IBillingProvider, StripeBillingProvider>(); services.AddScoped<BillingProviderRegistry>();
        services.AddScoped<IBusinessUnitOfWork, BusinessUnitOfWork>(); services.AddScoped<ICrmRepository, CrmRepository>(); services.AddScoped<ISupportRepository, SupportRepository>(); services.AddScoped<IKnowledgeAiRepository, KnowledgeAiRepository>(); services.AddScoped<IWorkflowAutomationRepository, WorkflowAutomationRepository>(); services.AddScoped<ITenantEntitlementRepository, TenantEntitlementRepository>(); services.AddScoped<IdentityEntitlementInboxProcessor>(); services.AddScoped<IGoldenPipelineProvisioner, GoldenPipelineProvisioner>(); services.AddScoped<IModuleProvisioner, GoldenPipelineModuleProvisioner>(); services.AddScoped<IModuleLifecycleHandler, GoldenPipelineModuleLifecycleHandler>(); services.AddScoped<IModuleRegistry, ModuleRegistry>(); services.AddScoped<IModuleProvisioningOrchestrator, ModuleProvisioningOrchestrator>(); services.AddScoped<IModuleDeactivationOrchestrator, ModuleDeactivationOrchestrator>(); services.AddScoped<ITenantLifecycleOrchestrator, TenantLifecycleOrchestrator>(); services.AddScoped<ILicenseChangeOrchestrator, LicenseChangeOrchestrator>(); services.AddHostedService<ModuleProvisioningRetryWorker>(); services.AddHostedService<LicenseExpirationWorker>(); services.AddHostedService<TenantLifecycleReconciliationWorker>();
        services.AddScoped<ITenantContext, TenantContext>(); services.AddScoped<IPasswordService, PasswordService>(); services.AddScoped<IKnowledgeRetriever, SqlKnowledgeRetriever>(); services.AddScoped<IAiProvider, LocalAiProvider>(); services.AddScoped<IAiTool, CreateLeadTool>(); services.AddScoped<IAiTool, CreateTicketTool>(); services.AddScoped<IAiTool, SearchKnowledgeTool>(); services.AddScoped<IAiToolRegistry, AiToolRegistry>(); services.AddScoped<IIntegrationRegistry, IntegrationRegistry>(); services.AddScoped<SalesAutomationService>(); services.AddScoped<DemoSeeder>(); services.AddScoped<CampaignExecutionService>(); services.AddScoped<ProspectReplyProcessingService>(); services.AddScoped<ProspectDiscoveryService>();
        services.AddHttpClient<SerpApiProspectDiscoveryProvider>(client => { client.BaseAddress=new Uri("https://serpapi.com/"); client.Timeout=TimeSpan.FromSeconds(60); }); services.AddScoped<IProspectDiscoveryProvider>(sp => sp.GetRequiredService<SerpApiProspectDiscoveryProvider>()); services.AddScoped<AutomationActionExecutor>(); services.AddScoped<RealisticScenarioService>(); services.AddScoped<IEmailDeliveryProvider, SmtpEmailProvider>(); services.AddHttpClient<BrevoEmailProvider>(client => { client.BaseAddress=new Uri("https://api.brevo.com/v3/"); client.Timeout=TimeSpan.FromSeconds(60); }); services.AddScoped<IEmailDeliveryProvider>(sp => sp.GetRequiredService<BrevoEmailProvider>()); services.AddHttpClient<SendGridEmailProvider>(); services.AddScoped<IEmailDeliveryProvider>(sp => sp.GetRequiredService<SendGridEmailProvider>()); services.AddScoped<EmailDeliveryService>(); return services;
    }
}
