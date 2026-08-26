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

var business = builder.AddProject<Projects.QualifyAI_Api>("business-api")
    .WithReference(businessDb)
    .WithReference(redis)
    .WithReference(rabbit);

var identity = builder.AddProject<Projects.QualifyAI_Identity_Api>("identity-api")
    .WithReference(identityDb)
    .WithReference(redis)
    .WithReference(rabbit)
    .WithEnvironment("Services__TenantManagement", business.GetEndpoint("http"));

builder.AddProject<Projects.QualifyAI_Automation_Api>("automation-api")
    .WithReference(automationDb).WithReference(redis).WithReference(rabbit);

builder.AddProject<Projects.QualifyAI_Notifications_Api>("notifications-api")
    .WithReference(notificationsDb).WithReference(redis).WithReference(rabbit);

builder.AddProject<Projects.QualifyAI_Knowledge_Api>("knowledge-api")
    .WithReference(knowledgeDb).WithReference(redis).WithReference(rabbit);

builder.AddProject<Projects.QualifyAI_AIOrchestration_Api>("aiorchestration-api")
    .WithReference(aiDb).WithReference(redis).WithReference(rabbit);

builder.AddProject<Projects.QualifyAI_Integrations_Api>("integrations-api")
    .WithReference(integrationsDb).WithReference(redis).WithReference(rabbit);

builder.Build().Run();
