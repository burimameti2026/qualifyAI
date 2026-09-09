using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using QualifyAI.Api;
using QualifyAI.Api.Security;
using QualifyAI.Api.Modules;
using QualifyAI.BuildingBlocks.Application.Behaviors;
using QualifyAI.BuildingBlocks.Application.Security;
using QualifyAI.BuildingBlocks.Security;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.It;
using QualifyAI.Infrastructure.WorkspacePackages;
using QualifyAI.Persistence.SqlServer.Projections;
using QualifyAI.Persistence.SqlServer.Queries;
using QualifyAI.BuildingBlocks.Security.Access;

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
    var configuredTenantId = configuration["TenantBootstrap:Renova:TenantId"];
    var renovaTenantId = Guid.TryParse(configuredTenantId, out var parsed)
        ? parsed
        : Guid.Parse("2f0c6e75-4df1-4bd5-bb49-6ef8ea0e3f1a");

    await using (var defaultScope = services.CreateAsyncScope())
    {
        var defaultDb = defaultScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await defaultDb.Database.MigrateAsync();
        await defaultDb.EnsureBillingSchemaAsync();

        var tenant = await defaultDb.Tenants.SingleOrDefaultAsync(x => x.Slug == renovaSlug);
        if (tenant is null)
        {
            tenant = new QualifyAI.Domain.Tenant
            {
                Id = renovaTenantId,
                Name = "Renova",
                Slug = renovaSlug,
                PlanCode = "enterprise",
                IsActive = true
            };
            defaultDb.Tenants.Add(tenant);
        }
        else if (tenant.Id != renovaTenantId)
        {
            renovaTenantId = tenant.Id;
        }

        var entitlement = await defaultDb.TenantEntitlements.SingleOrDefaultAsync(x => x.TenantId == renovaTenantId);
        if (entitlement is null)
        {
            defaultDb.TenantEntitlements.Add(new TenantEntitlementProjection
            {
                TenantId = renovaTenantId,
                TenantSlug = renovaSlug,
                TenantStatus = "active",
                LicensePlan = "enterprise",
                LicenseStatus = "active",
                MaxUsers = 100,
                StartsAtUtc = DateTime.UtcNow.AddMinutes(-5),
                ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
                Version = 1,
                ModulesJson = System.Text.Json.JsonSerializer.Serialize(QualifyAiModules.Enterprise),
                LimitsJson = "{\"users\":100}",
                UpdatedAtUtc = DateTime.UtcNow
            });
        }

        await defaultDb.SaveChangesAsync();
    }

    await using var renovaScope = services.CreateAsyncScope();
    var tenantContext = renovaScope.ServiceProvider.GetRequiredService<ITenantContext>();
    tenantContext.Set(new CurrentTenant(renovaTenantId, renovaSlug));

    var renovaDb = renovaScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await renovaDb.Database.MigrateAsync();
    await renovaDb.EnsureBillingSchemaAsync();

    // Tenant-scoped middleware evaluates entitlement data against the routed Renova database.
    // Keep a local projection there as well as the control-plane projection in the default DB.
    var renovaEntitlement = await renovaDb.TenantEntitlements.SingleOrDefaultAsync(x => x.TenantId == renovaTenantId);
    if (renovaEntitlement is null)
    {
        renovaDb.TenantEntitlements.Add(new TenantEntitlementProjection
        {
            TenantId = renovaTenantId,
            TenantSlug = renovaSlug,
            TenantStatus = "active",
            LicensePlan = "enterprise",
            LicenseStatus = "active",
            MaxUsers = 100,
            StartsAtUtc = DateTime.UtcNow.AddMinutes(-5),
            ExpiresAtUtc = DateTime.UtcNow.AddYears(1),
            Version = 1,
            ModulesJson = System.Text.Json.JsonSerializer.Serialize(QualifyAiModules.Enterprise),
            LimitsJson = "{\"users\":100}",
            UpdatedAtUtc = DateTime.UtcNow
        });
        await renovaDb.SaveChangesAsync();
    }

    var seeded = await renovaScope.ServiceProvider.GetRequiredService<RenovaDemoSeeder>().SeedAsync(renovaTenantId);

    logger.LogInformation(
        "Renova tenant database initialized. Database={Database}; SeededDemo={SeededDemo}; TenantId={TenantId}.",
        "RenovaPromotions",
        seeded,
        renovaTenantId);
}