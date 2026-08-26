using QualifyAI.AIOrchestration.Infrastructure.Persistence;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;
using QualifyAI.AIOrchestration.Application;
using QualifyAI.AIOrchestration.Infrastructure;
using QualifyAI.AIOrchestration.Api.Endpoints.Agents;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddAIOrchestrationApplication();
builder.Services.AddAIOrchestrationInfrastructure(builder.Configuration);
builder.Services.AddQualifyAiMessaging(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.MapCreateAgent();
app.MapGetAgent();
using(var scope=app.Services.CreateScope())
{
    var db=scope.ServiceProvider.GetRequiredService<AIOrchestrationDbContext>();
    await db.Database.EnsureCreatedAsync();
}
app.Run();
