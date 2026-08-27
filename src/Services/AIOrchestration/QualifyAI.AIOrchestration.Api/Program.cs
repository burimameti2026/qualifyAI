using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.AIOrchestration.Api.Endpoints.Agents;
using QualifyAI.AIOrchestration.Application;
using QualifyAI.AIOrchestration.Infrastructure;
using QualifyAI.AIOrchestration.Infrastructure.Messaging;
using QualifyAI.AIOrchestration.Infrastructure.Persistence;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddAIOrchestrationApplication();
builder.Services.AddAIOrchestrationInfrastructure(builder.Configuration);
builder.Services.AddQualifyAiMessaging(builder.Configuration, x => x.AddConsumer<IdentityEntitlementConsumer>());

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.MapCreateAgent();
app.MapGetAgent();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AIOrchestrationDbContext>();
    var hasMigrations = db.Database.GetMigrations().Any();
    if (hasMigrations) await db.Database.MigrateAsync();
    else if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("DatabaseBootstrap:AllowEnsureCreatedWithoutMigrations")) await db.Database.EnsureCreatedAsync();
    else throw new InvalidOperationException("AI Orchestration database has no EF migrations. Refusing production startup.");
}
app.Run();
