using MassTransit;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.MassTransit;
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

var app = builder.Build();
app.MapDefaultEndpoints();
app.UseSwagger();
app.UseSwaggerUI();
app.MapCreateNotification();
app.MapGetNotification();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationsDbContext>();
    var hasMigrations = db.Database.GetMigrations().Any();
    if (hasMigrations) await db.Database.MigrateAsync();
    else if (app.Environment.IsDevelopment() || builder.Configuration.GetValue<bool>("DatabaseBootstrap:AllowEnsureCreatedWithoutMigrations")) await db.Database.EnsureCreatedAsync();
    else throw new InvalidOperationException("Notifications database has no EF migrations. Refusing production startup.");
}
app.Run();
