using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.AIOrchestration.Api.Endpoints.Agents;
using QualifyAI.AIOrchestration.Application;
using QualifyAI.AIOrchestration.Infrastructure;
using QualifyAI.AIOrchestration.Infrastructure.Messaging;
using QualifyAI.AIOrchestration.Infrastructure.Persistence;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;
using QualifyAI.BuildingBlocks.Security;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddAIOrchestrationApplication();
builder.Services.AddAIOrchestrationInfrastructure(builder.Configuration);
builder.Services.AddQualifyAiMessaging(builder.Configuration, x => x.AddConsumer<IdentityEntitlementConsumer>());
builder.Services.AddQualifyAiResourceServer(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
var secured = app.MapGroup("").RequireAuthorization();
secured.MapCreateAgent();
secured.MapGetAgent();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AIOrchestrationDbContext>();
    await db.Database.MigrateAsync();
}
app.Run();
