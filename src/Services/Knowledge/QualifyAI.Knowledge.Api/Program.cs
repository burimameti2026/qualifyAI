using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;
using QualifyAI.BuildingBlocks.Security;
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
builder.Services.AddQualifyAiResourceServer(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
var secured = app.MapGroup("").RequireAuthorization();
secured.MapCreateKnowledgeBase();
secured.MapGetKnowledgeBase();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
    await db.Database.MigrateAsync();
}
app.Run();
