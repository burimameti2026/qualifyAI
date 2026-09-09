using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using QualifyAI.Api;
using QualifyAI.Api.Security;
using QualifyAI.Api.Modules;
using QualifyAI.Application;
using QualifyAI.BuildingBlocks.Application.Behaviors;
using QualifyAI.BuildingBlocks.Application.Security;
using QualifyAI.BuildingBlocks.Security;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.It;
using QualifyAI.Infrastructure.WorkspacePackages;
using QualifyAI.Persistence.SqlServer;
using QualifyAI.Persistence.SqlServer.Queries;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddMemoryCache();
builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssemblies(typeof(DashboardOverviewQueryHandler).Assembly, typeof(ListCompaniesQueryHandler).Assembly));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TenantValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissionAuthorizationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LicenseEntitlementBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ModuleEntitlementBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddBusinessInfrastructure(builder.Configuration, builder.Environment.IsDevelopment());
builder.Services.AddPlatformModules(builder.Configuration);
builder.Services.AddScoped<IRequestSecurityContext, BusinessRequestSecurityContext>();
builder.Services.AddScoped<IAuthorizationHandler, ModuleAuthorizationHandler>();
builder.Services.AddScoped<LeadQualificationService>();
builder.Services.AddScoped<WorkflowEngine>();
builder.Services.AddScoped<SlaService>();
builder.Services.AddScoped<KnowledgeGapService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddScoped<WorkspacePackageInstaller>();
builder.Services.AddScoped<FusionFleetPackageProvisioner>();
builder.Services.AddScoped<QualifyAiAcquisitionPackageProvisioner>();
builder.Services.AddHttpClient<IIntegrationProvider, GenericWebhookIntegration>();
builder.Services.Configure<RevenueAutomationOptions>(builder.Configuration.GetSection("RevenueAutomation"));
builder.Services.AddHostedService<RevenueAutomationWorker>();
builder.Services.AddHostedService<AcquisitionCampaignWorker>();
builder.Services.Configure<AutomationSchedulerOptions>(builder.Configuration.GetSection("AutomationScheduler"));
builder.Services.AddHostedService<AutomationSchedulerWorker>();
builder.Services.Configure<AutomationRetryOptions>(builder.Configuration.GetSection("AutomationRetry"));
builder.Services.AddHostedService<AutomationRetryWorker>();
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title="QualifyAI Business API", Version="v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type=SecuritySchemeType.Http, Scheme="bearer", BearerFormat="JWT" });
});
builder.Services.AddQualifyAiResourceServer(builder.Configuration);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<TenantEntitlementEnforcementMiddleware>();
app.UseMiddleware<TenantAccessMiddleware>();
app.UseMiddleware<BillingQuotaMiddleware>();
app.UseAuthorization();
app.MapControllers();
app.MapTenantLifecycle();
app.MapBillingWebhooks();
app.MapBillingQueries();
app.MapBillingVerification();
app.MapBillingHardening();
app.MapBillingHardeningVerification();
app.MapAutonomousAcquisition();
app.MapAutonomousAcquisitionVerification();
app.MapAutonomousAcquisitionE2e();
app.MapRealWorkspace();
app.MapHub<ConversationHub>("/hubs/conversations");
app.MapPublicChat();
app.MapExtendedAdmin();
app.MapPlatformModules();

await BootstrapBusinessDatabasesAsync(app.Services, builder.Configuration, app.Logger);

app.Run();

static async Task BootstrapBusinessDatabasesAsync(
    IServiceProvider services,
    IConfiguration configuration,
    ILogger logger)
{
    const string renovaSlug = "renova";

    await using (var defaultScope = services.CreateAsyncScope())
    {
        var defaultDb = defaultScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await defaultDb.Database.MigrateAsync();
        await defaultDb.EnsureBillingSchemaAsync();
    }

    if (!configuration.GetSection("TenantDatabases").GetChildren()
        .Any(x => x.Key.Equals(renovaSlug, StringComparison.OrdinalIgnoreCase)))
        return;

    Guid? renovaTenantId = null;
    for (var attempt = 1; attempt <= 30 && renovaTenantId is null; attempt++)
    {
        await using var lookupScope = services.CreateAsyncScope();
        var lookupDb = lookupScope.ServiceProvider.GetRequiredService<AppDbContext>();
        renovaTenantId = await lookupDb.Tenants
            .Where(x => x.Slug == renovaSlug)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync();

        if (renovaTenantId is null)
        {
            logger.LogInformation("Waiting for tenant projection '{TenantSlug}' before initializing its database (attempt {Attempt}/30).", renovaSlug, attempt);
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }

    if (renovaTenantId is not Guid tenantId)
    {
        logger.LogWarning("Tenant '{TenantSlug}' is not present in the business tenant projection yet. The dedicated database will initialize after the tenant is provisioned.", renovaSlug);
        return;
    }

    await using (var renovaScope = services.CreateAsyncScope())
    {
        var tenantContext = renovaScope.ServiceProvider.GetRequiredService<ITenantContext>();
        tenantContext.Set(new CurrentTenant(tenantId, renovaSlug));

        var renovaDb = renovaScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await renovaDb.Database.MigrateAsync();
        await renovaDb.EnsureBillingSchemaAsync();
        var seeded = await renovaScope.ServiceProvider.GetRequiredService<RenovaDemoSeeder>().SeedAsync(tenantId);

        logger.LogInformation("Renova tenant database initialized. Database={Database}; SeededDemo={SeededDemo}; TenantId={TenantId}.", "RenovaPromotions", seeded, tenantId);
    }
}
