using LeadsAI.Api;
using LeadsAI.Api.Modules;
using LeadsAI.Api.Security;
using LeadsAI.BuildingBlocks.Application.Behaviors;
using LeadsAI.BuildingBlocks.Application.Security;
using LeadsAI.BuildingBlocks.Security;
using LeadsAI.Infrastructure;
using LeadsAI.Infrastructure.Demo;
using LeadsAI.Infrastructure.WorkspacePackages;
using LeadsAI.Persistence.SqlServer.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

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
builder.Services.AddScoped<LeadsAI.Infrastructure.WorkspacePackages.RealWorkspaceService>();
builder.Services.AddScoped<WorkspacePackageInstaller>();

builder.Services.AddScoped<RealisticScenarioService>();
builder.Services.AddHttpClient<IIntegrationProvider, GenericWebhookIntegration>();
builder.Services.Configure<RevenueAutomationOptions>(builder.Configuration.GetSection("RevenueAutomation"));
builder.Services.AddHostedService<RevenueAutomationWorker>();
builder.Services.Configure<AutomationSchedulerOptions>(builder.Configuration.GetSection("AutomationScheduler"));
builder.Services.AddHostedService<AutomationSchedulerWorker>();
builder.Services.Configure<AutomationRetryOptions>(builder.Configuration.GetSection("AutomationRetry"));
builder.Services.AddHostedService<AutomationRetryWorker>();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<PlatformApiExceptionHandler>();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo { Title="FindLeadsAI Business API", Version="v1" });
    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme { Type=SecuritySchemeType.Http, Scheme="bearer", BearerFormat="JWT" });
});
builder.Services.AddQualifyAiResourceServer(builder.Configuration);
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();
app.UseExceptionHandler();
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
app.MapAcquisitionTenantSettings();
app.MapAutonomousAcquisitionVerification();
app.MapAutonomousAcquisitionE2e();
app.MapRealWorkspace();
app.MapCatalog();
app.MapEnterpriseOperations();
app.MapFinance();
app.MapAutomationAnalytics();
app.MapHub<ConversationHub>("/hubs/conversations");
app.MapPublicChat();
app.MapExtendedAdmin();
app.MapPlatformModules();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    await db.Database.MigrateAsync();
    await db.EnsureBillingSchemaAsync();
    await scope.ServiceProvider.MigratePlatformModuleDatabasesAsync();
}

app.Run();
