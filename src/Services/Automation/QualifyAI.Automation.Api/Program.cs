using MassTransit;
using QualifyAI.Automation.Api.Endpoints.AutomationDefinitions;
using QualifyAI.Automation.Application;
using QualifyAI.Automation.Infrastructure;
using QualifyAI.Automation.Infrastructure.Messaging;
using QualifyAI.Automation.Infrastructure.Persistence;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddAutomationApplication();
builder.Services.AddAutomationInfrastructure(builder.Configuration);
builder.Services.AddQualifyAiMessaging(builder.Configuration, x => x.AddConsumer<IdentityEntitlementConsumer>());

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.MapCreateAutomationDefinition();
app.MapGetAutomationDefinition();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AutomationDbContext>();
    await db.Database.EnsureCreatedAsync();
}
app.Run();
