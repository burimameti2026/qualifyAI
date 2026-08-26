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

// OAuth token exchange remains on the OpenIddict passthrough endpoint.
app.MapTokenEndpoint();

app.Run();
