using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using QualifyAI.Api;
using QualifyAI.Api.Security;
using QualifyAI.Application;
using QualifyAI.BuildingBlocks.Application.Behaviors;
using QualifyAI.BuildingBlocks.Application.Security;
using QualifyAI.BuildingBlocks.Security;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<DashboardOverviewQueryHandler>());
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(TenantValidationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PermissionAuthorizationBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LicenseEntitlementBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ModuleEntitlementBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddBusinessInfrastructure(builder.Configuration);
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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasMigrations = db.Database.GetMigrations().Any();
    if (hasMigrations) await db.Database.MigrateAsync();
    else if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("DatabaseBootstrap:AllowEnsureCreatedWithoutMigrations")) await db.Database.EnsureCreatedAsync();
    else throw new InvalidOperationException("Business database has no EF migrations. Refusing production startup.");

    if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("DemoSeed:Enabled"))
        await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
}

app.Run();
