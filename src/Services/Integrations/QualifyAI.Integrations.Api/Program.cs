using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;
using QualifyAI.Integrations.Api.Endpoints.Integrations;
using QualifyAI.Integrations.Application;
using QualifyAI.Integrations.Infrastructure;
using QualifyAI.Integrations.Infrastructure.Messaging;
using QualifyAI.Integrations.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddIntegrationsApplication();
builder.Services.AddIntegrationsInfrastructure(builder.Configuration);
builder.Services.AddQualifyAiMessaging(builder.Configuration, x => x.AddConsumer<IdentityEntitlementConsumer>());

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.MapCreateIntegration();
app.MapGetIntegration();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>();
    await db.Database.MigrateAsync();
}
app.Run();
