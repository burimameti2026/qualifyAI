using LeadsAI.BuildingBlocks.Messaging.MassTransit;
using MediatR;
using LeadsAI.Identity.Api;
using LeadsAI.Identity.Application;
using LeadsAI.Identity.Infrastructure;
using LeadsAI.BuildingBlocks.Application.Behaviors;
using LeadsAi.Identity.Api.Endpoints.Authentication;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<IdentityApiExceptionHandler>();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddIdentityApplication();
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
builder.Services.AddIdentityInfrastructure(
    builder.Configuration,
    builder.Environment.IsDevelopment());
builder.Services.AddQualifyAiMessaging(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// OAuth token exchange remains on the OpenIddict passthrough endpoint.
app.MapTokenEndpoint();

app.Run();
