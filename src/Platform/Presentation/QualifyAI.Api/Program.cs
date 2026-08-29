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
using QualifyAI.Persistence.SqlServer;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<DashboardOverviewQueryHandler>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TenantValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissionAuthorizationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LicenseEntitlementBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ModuleEntitlementBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddBusinessInfrastructure(
    builder.Configuration,
    builder.Environment.IsDevelopment());
builder.Services.AddPlatformModules(builder.Configuration);
builder.Services.AddScoped<IRequestSecurityContext, BusinessRequestSecurityContext>();
builder.Services.AddScoped<IAuthorizationHandler, ModuleAuthorizationHandler>();

builder.Services.AddScoped<LeadQualificationService>();
builder.Services.AddScoped<WorkflowEngine>();
builder.Services.AddScoped<SlaService>();
builder.Services.AddScoped<KnowledgeGapService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddHttpClient<IIntegrationProvider, GenericWebhookIntegration>();

builder.Services.Configure<RevenueAutomationOptions>(
    builder.Configuration.GetSection("RevenueAutomation"));
builder.Services.AddHostedService<RevenueAutomationWorker>();
builder.Services.AddHostedService<AcquisitionCampaignWorker>();
builder.Services.Configure<AutomationSchedulerOptions>(builder.Configuration.GetSection("AutomationScheduler"));
builder.Services.AddHostedService<AutomationSchedulerWorker>();
builder.Services.Configure<AutomationRetryOptions>(builder.Configuration.GetSection("AutomationRetry"));
builder.Services.AddHostedService<AutomationRetryWorker>();

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QualifyAI Business API",
        Version = "v1"
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
});

builder.Services.AddQualifyAiResourceServer(builder.Configuration);
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin()));

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseMiddleware<TenantMiddleware>();
app.UseMiddleware<TenantEntitlementEnforcementMiddleware>();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ConversationHub>("/hubs/conversations");
app.MapPublicChat();
app.MapExtendedAdmin();
app.MapPlatformModules();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await scope.ServiceProvider.MigratePlatformModuleDatabasesAsync();

    if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("DemoSeed:Enabled"))
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
}

app.Run();
