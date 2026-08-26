using QualifyAI.BuildingBlocks.Messaging.MassTransit;
using QualifyAI.Identity.Application;
using QualifyAI.Identity.Infrastructure;
using QualifyAI.Identity.Api.Endpoints.Authentication;
using QualifyAI.Identity.Api.Endpoints.Users;

var builder=WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
builder.Services.AddQualifyAiMessaging(builder.Configuration);

var app=builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapTokenEndpoint();
app.MapRecoveryEndpoints();
app.MapUserAdmin();
app.Run();
