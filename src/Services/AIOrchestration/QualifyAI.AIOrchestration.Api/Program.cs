using MassTransit;
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
    await db.Database.EnsureCreatedAsync();
}
app.Run();
