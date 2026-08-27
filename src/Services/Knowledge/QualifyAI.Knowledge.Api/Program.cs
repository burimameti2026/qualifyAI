using MassTransit;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;
using QualifyAI.Knowledge.Api.Endpoints.KnowledgeBases;
using QualifyAI.Knowledge.Application;
using QualifyAI.Knowledge.Infrastructure;
using QualifyAI.Knowledge.Infrastructure.Messaging;
using QualifyAI.Knowledge.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddKnowledgeApplication();
builder.Services.AddKnowledgeInfrastructure(builder.Configuration);
builder.Services.AddQualifyAiMessaging(builder.Configuration, x => x.AddConsumer<IdentityEntitlementConsumer>());

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.MapCreateKnowledgeBase();
app.MapGetKnowledgeBase();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
    await db.Database.EnsureCreatedAsync();
}
app.Run();
