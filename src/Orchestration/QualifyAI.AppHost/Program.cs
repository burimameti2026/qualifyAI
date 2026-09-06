var builder = DistributedApplication.CreateBuilder(args);

var sql = builder.AddSqlServer("sql").WithDataVolume();
var redis = builder.AddRedis("redis");
var rabbit = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();

var businessDb = sql.AddDatabase("DefaultConnection");
var identityDb = sql.AddDatabase("IdentityDb");
var automationDb = sql.AddDatabase("AutomationDb");
var notificationsDb = sql.AddDatabase("NotificationsDb");
var knowledgeDb = sql.AddDatabase("KnowledgeDb");
var aiDb = sql.AddDatabase("AIOrchestrationDb");
var integrationsDb = sql.AddDatabase("IntegrationsDb");

var business = builder.AddProject<Projects.QualifyAI_Api>("platform-api")
    .WithReference(businessDb)
    .WithReference(automationDb)
    .WithReference(notificationsDb)
    .WithReference(knowledgeDb)
    .WithReference(aiDb)
    .WithReference(integrationsDb)
    .WithReference(redis)
    .WithReference(rabbit);

var identity = builder.AddProject<Projects.QualifyAI_Identity_Api>("identity-api")
    .WithReference(identityDb)
    .WithReference(redis)
    .WithReference(rabbit);

builder.AddProject<Projects.QualifyAI_ApiGateway>("api-gateway")
    .WithEnvironment(
        "ReverseProxy__Clusters__platform__Destinations__primary__Address",
        business.GetEndpoint("http"))
    .WithEnvironment(
        "ReverseProxy__Clusters__identity__Destinations__primary__Address",
        identity.GetEndpoint("http"));

builder.Build().Run();
