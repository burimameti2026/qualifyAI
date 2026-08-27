using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;
using QualifyAI.BuildingBlocks.Security;
using QualifyAI.Notifications.Api.Endpoints.Notifications;
using QualifyAI.Notifications.Application;
using QualifyAI.Notifications.Infrastructure;
using QualifyAI.Notifications.Infrastructure.Messaging;
using QualifyAI.Notifications.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.AddSwaggerGen();
builder.Services.AddNotificationsApplication();
builder.Services.AddNotificationsInfrastructure(builder.Configuration);
builder.Services.AddQualifyAiMessaging(builder.Configuration, x => x.AddConsumer<IdentityEntitlementConsumer>());
builder.Services.AddQualifyAiResourceServer(builder.Configuration);

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
var secured = app.MapGroup("").RequireAuthorization();
secured.MapCreateNotification();
secured.MapGetNotification();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    await db.Database.MigrateAsync();
}
app.Run();
