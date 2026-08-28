using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.AIOrchestration.Application;
using QualifyAI.AIOrchestration.Infrastructure;
using QualifyAI.AIOrchestration.Infrastructure.Persistence;
using QualifyAI.Api.Modules.AIOrchestration.Endpoints;
using QualifyAI.Api.Modules.Automation.Endpoints;
using QualifyAI.Api.Modules.Integrations.Endpoints;
using QualifyAI.Api.Modules.Knowledge.Endpoints;
using QualifyAI.Api.Modules.Notifications.Endpoints;
using QualifyAI.Automation.Application;
using QualifyAI.Automation.Infrastructure;
using QualifyAI.Automation.Infrastructure.Persistence;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;
using QualifyAI.Infrastructure.Messaging.Consumers;
using QualifyAI.Integrations.Application;
using QualifyAI.Integrations.Infrastructure;
using QualifyAI.Integrations.Infrastructure.Persistence;
using QualifyAI.Knowledge.Application;
using QualifyAI.Knowledge.Infrastructure;
using QualifyAI.Knowledge.Infrastructure.Persistence;
using QualifyAI.Notifications.Application;
using QualifyAI.Notifications.Infrastructure;
using QualifyAI.Notifications.Infrastructure.Persistence;
using AIEntitlementConsumer = QualifyAI.AIOrchestration.Infrastructure.Messaging.IdentityEntitlementConsumer;
using AutomationEntitlementConsumer = QualifyAI.Automation.Infrastructure.Messaging.IdentityEntitlementConsumer;
using IntegrationsEntitlementConsumer = QualifyAI.Integrations.Infrastructure.Messaging.IdentityEntitlementConsumer;
using KnowledgeEntitlementConsumer = QualifyAI.Knowledge.Infrastructure.Messaging.IdentityEntitlementConsumer;
using NotificationsEntitlementConsumer = QualifyAI.Notifications.Infrastructure.Messaging.IdentityEntitlementConsumer;

namespace QualifyAI.Api.Modules;

public static class ModuleRegistration
{
    public static IServiceCollection AddPlatformModules(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddAutomationApplication();
        services.AddAutomationInfrastructure(configuration);
        services.AddNotificationsApplication();
        services.AddNotificationsInfrastructure(configuration);
        services.AddKnowledgeApplication();
        services.AddKnowledgeInfrastructure(configuration);
        services.AddAIOrchestrationApplication();
        services.AddAIOrchestrationInfrastructure(configuration);
        services.AddIntegrationsApplication();
        services.AddIntegrationsInfrastructure(configuration);

        services.AddQualifyAiMessaging(configuration, bus =>
        {
            bus.AddConsumer<TenantCreatedConsumer>();
            bus.AddConsumer<TenantStatusChangedConsumer>();
            bus.AddConsumer<TenantLicenseChangedConsumer>();
            bus.AddConsumer<AutomationEntitlementConsumer>();
            bus.AddConsumer<NotificationsEntitlementConsumer>();
            bus.AddConsumer<KnowledgeEntitlementConsumer>();
            bus.AddConsumer<AIEntitlementConsumer>();
            bus.AddConsumer<IntegrationsEntitlementConsumer>();
        });

        return services;
    }

    public static IEndpointRouteBuilder MapPlatformModules(this IEndpointRouteBuilder endpoints)
    {
        var modules = endpoints.MapGroup("/api/modules").RequireAuthorization();

        var automation = modules.MapGroup("/automation");
        automation.MapCreateAutomationDefinition();
        automation.MapGetAutomationDefinition();

        var notifications = modules.MapGroup("/notifications");
        notifications.MapCreateNotification();
        notifications.MapGetNotification();

        var knowledge = modules.MapGroup("/knowledge");
        knowledge.MapCreateKnowledgeBase();
        knowledge.MapGetKnowledgeBase();

        var ai = modules.MapGroup("/ai");
        ai.MapCreateAgent();
        ai.MapGetAgent();

        var integrations = modules.MapGroup("/integrations");
        integrations.MapCreateIntegration();
        integrations.MapGetIntegration();

        return endpoints;
    }

    public static async Task MigratePlatformModuleDatabasesAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        await services.GetRequiredService<AutomationDbContext>()
            .Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<NotificationsDbContext>()
            .Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<KnowledgeDbContext>()
            .Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<AIOrchestrationDbContext>()
            .Database.MigrateAsync(cancellationToken);
        await services.GetRequiredService<IntegrationsDbContext>()
            .Database.MigrateAsync(cancellationToken);
    }
}
