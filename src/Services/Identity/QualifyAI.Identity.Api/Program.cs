using QualifyAI.BuildingBlocks.Messaging.MassTransit;
using QualifyAI.Identity.Api.Endpoints.Authentication;
using QualifyAI.Identity.Application;
using QualifyAI.Identity.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddQualifyAiMessaging(builder.Configuration);

var app = builder.Build();

app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Authentication/recovery are the final legacy minimal endpoints and will be
// migrated separately to avoid changing the OAuth/OpenIddict contract mid-pass.
app.MapTokenEndpoint();
app.MapRecoveryEndpoints();

app.Run();
