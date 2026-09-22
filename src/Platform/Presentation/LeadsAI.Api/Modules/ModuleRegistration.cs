using MassTransit;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using LeadsAI.AIOrchestration.Application;
using LeadsAI.AIOrchestration.Infrastructure;
using LeadsAI.AIOrchestration.Persistence.SqlServer;
using LeadsAI.Api.Modules.AIOrchestration.Endpoints;
using LeadsAI.Api.Modules.Automation.Endpoints;
using LeadsAI.Api.Modules.Integrations.Endpoints;
using LeadsAI.Api.Modules.Knowledge.Endpoints;
using LeadsAI.Api.Modules.Notifications.Endpoints;
using LeadsAI.Automation.Application;
using LeadsAI.Automation.Infrastructure;
using LeadsAI.Automation.Persistence.SqlServer;
using LeadsAI.BuildingBlocks.Messaging.MassTransit;
using LeadsAI.Infrastructure.Messaging.Consumers;
using LeadsAI.Integrations.Application;
using LeadsAI.Integrations.Infrastructure;
using LeadsAI.Integrations.Persistence.SqlServer;
using LeadsAI.Knowledge.Application;
using LeadsAI.Knowledge.Infrastructure;
using LeadsAI.Knowledge.Persistence.SqlServer;
using LeadsAI.Notifications.Application;
using LeadsAI.Notifications.Infrastructure;
using LeadsAI.Notifications.Persistence.SqlServer;
using AIEntitlementConsumer = LeadsAI.AIOrchestration.Infrastructure.Messaging.IdentityEntitlementConsumer;
using AutomationEntitlementConsumer = LeadsAI.Automation.Infrastructure.Messaging.IdentityEntitlementConsumer;
using IntegrationsEntitlementConsumer = LeadsAI.Integrations.Infrastructure.Messaging.IdentityEntitlementConsumer;
using KnowledgeEntitlementConsumer = LeadsAI.Knowledge.Infrastructure.Messaging.IdentityEntitlementConsumer;
using NotificationsEntitlementConsumer = LeadsAI.Notifications.Infrastructure.Messaging.IdentityEntitlementConsumer;

namespace LeadsAI.Api.Modules;

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
            bus.AddConsumer<AutomationTriggerConsumer>();
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
        await using var scope = services.CreateAsyncScope();
        var scopedServices = scope.ServiceProvider;

        await MigrateDatabaseAsync(
            scopedServices.GetRequiredService<AutomationDbContext>(),
            cancellationToken);

        await MigrateDatabaseAsync(
            scopedServices.GetRequiredService<NotificationsDbContext>(),
            cancellationToken);

        await MigrateDatabaseAsync(
            scopedServices.GetRequiredService<KnowledgeDbContext>(),
            cancellationToken);

        await MigrateDatabaseAsync(
            scopedServices.GetRequiredService<AIOrchestrationDbContext>(),
            cancellationToken);

        await MigrateDatabaseAsync(
            scopedServices.GetRequiredService<IntegrationsDbContext>(),
            cancellationToken);
    }

    private static async Task MigrateDatabaseAsync(
        DbContext db,
        CancellationToken cancellationToken)
    {
        const int maxAttempts = 5;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (SqlException ex) when (ex.Number == 1801 && attempt < maxAttempts)
            {
                await Task.Delay(TimeSpan.FromSeconds(attempt), cancellationToken);
            }
        }
    }
}
