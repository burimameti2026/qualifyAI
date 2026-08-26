using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using QualifyAI.Api;
using QualifyAI.Application;
using QualifyAI.BuildingBlocks.Security;
using QualifyAI.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssemblyContaining<DashboardOverviewQueryHandler>());

builder.Services.AddBusinessInfrastructure(builder.Configuration);

builder.Services.AddScoped<LeadQualificationService>();
builder.Services.AddScoped<WorkflowEngine>();
builder.Services.AddScoped<SlaService>();
builder.Services.AddScoped<KnowledgeGapService>();
builder.Services.AddScoped<BillingService>();
builder.Services.AddHttpClient<IIntegrationProvider, GenericWebhookIntegration>();

builder.Services.Configure<RevenueAutomationOptions>(
    builder.Configuration.GetSection("RevenueAutomation"));
builder.Services.AddHostedService<RevenueAutomationWorker>();

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
app.UseAuthorization();

app.MapHub<ConversationHub>("/hubs/conversations");
app.MapPublicChat();
app.MapModules();
app.MapExtendedAdmin();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    if (app.Environment.IsDevelopment())
        await db.Database.EnsureCreatedAsync();
    else
        await db.Database.MigrateAsync();

    await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync();
}

app.Run();
