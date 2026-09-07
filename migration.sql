IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AgencyClients] (
    [Id] uniqueidentifier NOT NULL,
    [AgencyId] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Label] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_AgencyClients] PRIMARY KEY ([Id])
);

CREATE TABLE [Agencys] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Slug] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Agencys] PRIMARY KEY ([Id])
);

CREATE TABLE [AiAgents] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Role] nvarchar(max) NOT NULL,
    [Instructions] nvarchar(max) NOT NULL,
    [Tone] nvarchar(max) NOT NULL,
    [Model] nvarchar(max) NOT NULL,
    [LanguagesCsv] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [KnowledgeBaseId] uniqueidentifier NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AiAgents] PRIMARY KEY ([Id])
);

CREATE TABLE [AiAgentVersions] (
    [Id] uniqueidentifier NOT NULL,
    [AgentId] uniqueidentifier NOT NULL,
    [Version] int NOT NULL,
    [ConfigurationJson] nvarchar(max) NOT NULL,
    [Published] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AiAgentVersions] PRIMARY KEY ([Id])
);

CREATE TABLE [AiToolDefinitions] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [InputSchemaJson] nvarchar(max) NOT NULL,
    [RequiredPermission] nvarchar(max) NOT NULL,
    [Enabled] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AiToolDefinitions] PRIMARY KEY ([Id])
);

CREATE TABLE [AiToolExecutions] (
    [Id] uniqueidentifier NOT NULL,
    [AgentId] uniqueidentifier NULL,
    [ConversationId] uniqueidentifier NULL,
    [ToolName] nvarchar(max) NOT NULL,
    [InputJson] nvarchar(max) NOT NULL,
    [OutputJson] nvarchar(max) NOT NULL,
    [Success] bit NOT NULL,
    [DurationMs] bigint NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AiToolExecutions] PRIMARY KEY ([Id])
);

CREATE TABLE [ApiKeys] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [KeyHash] nvarchar(max) NOT NULL,
    [ExpiresAtUtc] datetime2 NULL,
    [Revoked] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ApiKeys] PRIMARY KEY ([Id])
);

CREATE TABLE [AppUsers] (
    [Id] uniqueidentifier NOT NULL,
    [Email] nvarchar(450) NOT NULL,
    [DisplayName] nvarchar(max) NOT NULL,
    [PasswordHash] nvarchar(max) NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AppUsers] PRIMARY KEY ([Id])
);

CREATE TABLE [Attachments] (
    [Id] uniqueidentifier NOT NULL,
    [MessageId] uniqueidentifier NULL,
    [FileName] nvarchar(max) NOT NULL,
    [ContentType] nvarchar(max) NOT NULL,
    [Url] nvarchar(max) NOT NULL,
    [SizeBytes] bigint NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Attachments] PRIMARY KEY ([Id])
);

CREATE TABLE [AuditLogs] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [Action] nvarchar(max) NOT NULL,
    [EntityType] nvarchar(max) NOT NULL,
    [EntityId] nvarchar(max) NOT NULL,
    [DataJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [AutomationRules] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Trigger] nvarchar(max) NOT NULL,
    [ConditionsJson] nvarchar(max) NOT NULL,
    [ActionsJson] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AutomationRules] PRIMARY KEY ([Id])
);

CREATE TABLE [AutomationRuns] (
    [Id] uniqueidentifier NOT NULL,
    [RuleId] uniqueidentifier NOT NULL,
    [TriggerDataJson] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [LogJson] nvarchar(max) NOT NULL,
    [CompletedAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AutomationRuns] PRIMARY KEY ([Id])
);

CREATE TABLE [BillingInvoices] (
    [Id] uniqueidentifier NOT NULL,
    [Number] nvarchar(max) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Currency] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [DueAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_BillingInvoices] PRIMARY KEY ([Id])
);

CREATE TABLE [BrandingProfiles] (
    [Id] uniqueidentifier NOT NULL,
    [ProductName] nvarchar(max) NOT NULL,
    [LogoUrl] nvarchar(max) NOT NULL,
    [PrimaryColor] nvarchar(max) NOT NULL,
    [AccentColor] nvarchar(max) NOT NULL,
    [SupportEmail] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_BrandingProfiles] PRIMARY KEY ([Id])
);

CREATE TABLE [Channels] (
    [Id] uniqueidentifier NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Enabled] bit NOT NULL,
    [SettingsJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Channels] PRIMARY KEY ([Id])
);

CREATE TABLE [Companys] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Domain] nvarchar(max) NOT NULL,
    [Industry] nvarchar(max) NOT NULL,
    [Employees] int NULL,
    [Country] nvarchar(max) NOT NULL,
    [AnnualRevenue] decimal(18,2) NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Companys] PRIMARY KEY ([Id])
);

CREATE TABLE [ConsentRecords] (
    [Id] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NULL,
    [Type] nvarchar(max) NOT NULL,
    [Granted] bit NOT NULL,
    [RecordedAtUtc] datetime2 NOT NULL,
    [Source] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ConsentRecords] PRIMARY KEY ([Id])
);

CREATE TABLE [Contacts] (
    [Id] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NULL,
    [FirstName] nvarchar(max) NOT NULL,
    [LastName] nvarchar(max) NOT NULL,
    [Email] nvarchar(max) NOT NULL,
    [Phone] nvarchar(max) NOT NULL,
    [LifecycleStage] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Contacts] PRIMARY KEY ([Id])
);

CREATE TABLE [ConversationNotes] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ConversationNotes] PRIMARY KEY ([Id])
);

CREATE TABLE [Conversations] (
    [Id] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NULL,
    [LeadId] uniqueidentifier NULL,
    [ChannelId] uniqueidentifier NULL,
    [Status] int NOT NULL,
    [AssignedUserId] uniqueidentifier NULL,
    [AiEnabled] bit NOT NULL,
    [LastMessageAtUtc] datetime2 NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Conversations] PRIMARY KEY ([Id])
);

CREATE TABLE [CrmActivitys] (
    [Id] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NULL,
    [CompanyId] uniqueidentifier NULL,
    [LeadId] uniqueidentifier NULL,
    [Type] nvarchar(max) NOT NULL,
    [Subject] nvarchar(max) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CrmActivitys] PRIMARY KEY ([Id])
);

CREATE TABLE [CrmTasks] (
    [Id] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NULL,
    [LeadId] uniqueidentifier NULL,
    [OwnerUserId] uniqueidentifier NULL,
    [Title] nvarchar(max) NOT NULL,
    [DueAtUtc] datetime2 NULL,
    [Completed] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CrmTasks] PRIMARY KEY ([Id])
);

CREATE TABLE [CsatResponses] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NOT NULL,
    [Score] int NOT NULL,
    [Comment] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CsatResponses] PRIMARY KEY ([Id])
);

CREATE TABLE [CustomDomains] (
    [Id] uniqueidentifier NOT NULL,
    [Host] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [VerificationToken] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CustomDomains] PRIMARY KEY ([Id])
);

CREATE TABLE [CustomFieldDefinitions] (
    [Id] uniqueidentifier NOT NULL,
    [EntityType] nvarchar(max) NOT NULL,
    [Key] nvarchar(max) NOT NULL,
    [Label] nvarchar(max) NOT NULL,
    [DataType] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CustomFieldDefinitions] PRIMARY KEY ([Id])
);

CREATE TABLE [CustomFieldValues] (
    [Id] uniqueidentifier NOT NULL,
    [DefinitionId] uniqueidentifier NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_CustomFieldValues] PRIMARY KEY ([Id])
);

CREATE TABLE [DataRetentionPolicys] (
    [Id] uniqueidentifier NOT NULL,
    [EntityType] nvarchar(max) NOT NULL,
    [RetentionDays] int NOT NULL,
    [Enabled] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_DataRetentionPolicys] PRIMARY KEY ([Id])
);

CREATE TABLE [EvaluationDatasets] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EvaluationDatasets] PRIMARY KEY ([Id])
);

CREATE TABLE [EvaluationResults] (
    [Id] uniqueidentifier NOT NULL,
    [RunId] uniqueidentifier NOT NULL,
    [TestCaseId] uniqueidentifier NOT NULL,
    [Accuracy] decimal(18,2) NOT NULL,
    [Groundedness] decimal(18,2) NOT NULL,
    [ToolCorrect] bit NOT NULL,
    [LatencyMs] bigint NOT NULL,
    [Cost] decimal(18,2) NOT NULL,
    [Notes] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EvaluationResults] PRIMARY KEY ([Id])
);

CREATE TABLE [EvaluationRuns] (
    [Id] uniqueidentifier NOT NULL,
    [DatasetId] uniqueidentifier NOT NULL,
    [AgentId] uniqueidentifier NULL,
    [Status] nvarchar(max) NOT NULL,
    [OverallScore] decimal(18,2) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EvaluationRuns] PRIMARY KEY ([Id])
);

CREATE TABLE [EvaluationTestCases] (
    [Id] uniqueidentifier NOT NULL,
    [DatasetId] uniqueidentifier NOT NULL,
    [Input] nvarchar(max) NOT NULL,
    [ExpectedAnswer] nvarchar(max) NOT NULL,
    [ExpectedTool] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_EvaluationTestCases] PRIMARY KEY ([Id])
);

CREATE TABLE [FieldMappings] (
    [Id] uniqueidentifier NOT NULL,
    [ConnectionId] uniqueidentifier NOT NULL,
    [EntityType] nvarchar(max) NOT NULL,
    [LocalField] nvarchar(max) NOT NULL,
    [RemoteField] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_FieldMappings] PRIMARY KEY ([Id])
);

CREATE TABLE [IcpProfiles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [CriteriaJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_IcpProfiles] PRIMARY KEY ([Id])
);

CREATE TABLE [InboxMessages] (
    [Id] uniqueidentifier NOT NULL,
    [Consumer] nvarchar(200) NOT NULL,
    [ReceivedAtUtc] datetime2 NOT NULL,
    [ProcessedAtUtc] datetime2 NULL,
    CONSTRAINT [PK_InboxMessages] PRIMARY KEY ([Id], [Consumer])
);

CREATE TABLE [IndustryPacks] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [TemplateJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_IndustryPacks] PRIMARY KEY ([Id])
);

CREATE TABLE [IntegrationConnections] (
    [Id] uniqueidentifier NOT NULL,
    [Provider] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [SettingsJson] nvarchar(max) NOT NULL,
    [SecretReference] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_IntegrationConnections] PRIMARY KEY ([Id])
);

CREATE TABLE [IntegrationSyncJobs] (
    [Id] uniqueidentifier NOT NULL,
    [ConnectionId] uniqueidentifier NOT NULL,
    [Direction] nvarchar(max) NOT NULL,
    [EntityType] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [Error] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_IntegrationSyncJobs] PRIMARY KEY ([Id])
);

CREATE TABLE [KnowledgeBases] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_KnowledgeBases] PRIMARY KEY ([Id])
);

CREATE TABLE [KnowledgeChunks] (
    [Id] uniqueidentifier NOT NULL,
    [DocumentId] uniqueidentifier NOT NULL,
    [ChunkIndex] int NOT NULL,
    [Text] nvarchar(max) NOT NULL,
    [VectorJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_KnowledgeChunks] PRIMARY KEY ([Id])
);

CREATE TABLE [KnowledgeDocuments] (
    [Id] uniqueidentifier NOT NULL,
    [KnowledgeBaseId] uniqueidentifier NOT NULL,
    [SourceId] uniqueidentifier NULL,
    [Title] nvarchar(max) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [Version] int NOT NULL,
    [Published] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_KnowledgeDocuments] PRIMARY KEY ([Id])
);

CREATE TABLE [KnowledgeGaps] (
    [Id] uniqueidentifier NOT NULL,
    [Topic] nvarchar(max) NOT NULL,
    [Occurrences] int NOT NULL,
    [ExampleQuestion] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [ImpactScore] decimal(18,2) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_KnowledgeGaps] PRIMARY KEY ([Id])
);

CREATE TABLE [KnowledgeSources] (
    [Id] uniqueidentifier NOT NULL,
    [KnowledgeBaseId] uniqueidentifier NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Location] nvarchar(max) NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [LastSyncedAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_KnowledgeSources] PRIMARY KEY ([Id])
);

CREATE TABLE [Leads] (
    [Id] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NULL,
    [Source] nvarchar(max) NOT NULL,
    [Score] int NOT NULL,
    [Temperature] int NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [EstimatedValue] decimal(18,2) NULL,
    [IntentSummary] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Leads] PRIMARY KEY ([Id])
);

CREATE TABLE [LeadScoreExplanations] (
    [Id] uniqueidentifier NOT NULL,
    [LeadId] uniqueidentifier NOT NULL,
    [Factor] nvarchar(max) NOT NULL,
    [Points] int NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_LeadScoreExplanations] PRIMARY KEY ([Id])
);

CREATE TABLE [MeetingBookings] (
    [Id] uniqueidentifier NOT NULL,
    [MeetingTypeId] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NULL,
    [LeadId] uniqueidentifier NULL,
    [HostUserId] uniqueidentifier NULL,
    [StartsAtUtc] datetime2 NOT NULL,
    [EndsAtUtc] datetime2 NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [ExternalEventId] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MeetingBookings] PRIMARY KEY ([Id])
);

CREATE TABLE [MeetingTypes] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [DurationMinutes] int NOT NULL,
    [LocationType] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MeetingTypes] PRIMARY KEY ([Id])
);

CREATE TABLE [Messages] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NOT NULL,
    [SenderType] nvarchar(max) NOT NULL,
    [SenderUserId] uniqueidentifier NULL,
    [Text] nvarchar(max) NOT NULL,
    [MetadataJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Messages] PRIMARY KEY ([Id])
);

CREATE TABLE [MetricSnapshots] (
    [Id] uniqueidentifier NOT NULL,
    [Metric] nvarchar(max) NOT NULL,
    [Value] decimal(18,2) NOT NULL,
    [PeriodStartUtc] datetime2 NOT NULL,
    [PeriodEndUtc] datetime2 NOT NULL,
    [DimensionsJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_MetricSnapshots] PRIMARY KEY ([Id])
);

CREATE TABLE [Notifications] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NULL,
    [Title] nvarchar(max) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [IsRead] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Notifications] PRIMARY KEY ([Id])
);

CREATE TABLE [Opportunitys] (
    [Id] uniqueidentifier NOT NULL,
    [LeadId] uniqueidentifier NULL,
    [CompanyId] uniqueidentifier NULL,
    [ContactId] uniqueidentifier NULL,
    [PipelineStageId] uniqueidentifier NULL,
    [Name] nvarchar(max) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Status] int NOT NULL,
    [ExpectedCloseUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Opportunitys] PRIMARY KEY ([Id])
);

CREATE TABLE [Permissions] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Permissions] PRIMARY KEY ([Id])
);

CREATE TABLE [PiiRedactionJobs] (
    [Id] uniqueidentifier NOT NULL,
    [EntityType] nvarchar(max) NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PiiRedactionJobs] PRIMARY KEY ([Id])
);

CREATE TABLE [Pipelines] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [IsDefault] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Pipelines] PRIMARY KEY ([Id])
);

CREATE TABLE [PipelineStages] (
    [Id] uniqueidentifier NOT NULL,
    [PipelineId] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [SortOrder] int NOT NULL,
    [Probability] decimal(18,2) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PipelineStages] PRIMARY KEY ([Id])
);

CREATE TABLE [Plans] (
    [Id] uniqueidentifier NOT NULL,
    [Code] nvarchar(max) NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [MonthlyPrice] decimal(18,2) NOT NULL,
    [Currency] nvarchar(max) NOT NULL,
    [EntitlementsJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Plans] PRIMARY KEY ([Id])
);

CREATE TABLE [PromptVersions] (
    [Id] uniqueidentifier NOT NULL,
    [AgentId] uniqueidentifier NOT NULL,
    [Version] int NOT NULL,
    [Prompt] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_PromptVersions] PRIMARY KEY ([Id])
);

CREATE TABLE [QualificationAnswers] (
    [Id] uniqueidentifier NOT NULL,
    [LeadId] uniqueidentifier NOT NULL,
    [Key] nvarchar(max) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [ScoreDelta] int NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_QualificationAnswers] PRIMARY KEY ([Id])
);

CREATE TABLE [QualificationFlows] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_QualificationFlows] PRIMARY KEY ([Id])
);

CREATE TABLE [RevenueAttributions] (
    [Id] uniqueidentifier NOT NULL,
    [LeadId] uniqueidentifier NULL,
    [OpportunityId] uniqueidentifier NULL,
    [ConversationId] uniqueidentifier NULL,
    [InfluencedRevenue] decimal(18,2) NOT NULL,
    [Model] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_RevenueAttributions] PRIMARY KEY ([Id])
);

CREATE TABLE [RolePermissions] (
    [Id] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [PermissionId] uniqueidentifier NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_RolePermissions] PRIMARY KEY ([Id])
);

CREATE TABLE [Roles] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Roles] PRIMARY KEY ([Id])
);

CREATE TABLE [SalesSequences] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SalesSequences] PRIMARY KEY ([Id])
);

CREATE TABLE [ScoringRules] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Field] nvarchar(max) NOT NULL,
    [Operator] nvarchar(max) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [Points] int NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_ScoringRules] PRIMARY KEY ([Id])
);

CREATE TABLE [SequenceEnrollments] (
    [Id] uniqueidentifier NOT NULL,
    [SequenceId] uniqueidentifier NOT NULL,
    [ContactId] uniqueidentifier NOT NULL,
    [CurrentStep] int NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [NextRunAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SequenceEnrollments] PRIMARY KEY ([Id])
);

CREATE TABLE [SequenceSteps] (
    [Id] uniqueidentifier NOT NULL,
    [SequenceId] uniqueidentifier NOT NULL,
    [StepNumber] int NOT NULL,
    [DelayMinutes] int NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [ConfigJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SequenceSteps] PRIMARY KEY ([Id])
);

CREATE TABLE [SlaPolicys] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [FirstResponseMinutes] int NOT NULL,
    [ResolutionMinutes] int NOT NULL,
    [BusinessHoursOnly] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SlaPolicys] PRIMARY KEY ([Id])
);

CREATE TABLE [SsoConfigurations] (
    [Id] uniqueidentifier NOT NULL,
    [ProviderType] nvarchar(max) NOT NULL,
    [EntityId] nvarchar(max) NOT NULL,
    [MetadataUrl] nvarchar(max) NOT NULL,
    [Enabled] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_SsoConfigurations] PRIMARY KEY ([Id])
);

CREATE TABLE [Subscriptions] (
    [Id] uniqueidentifier NOT NULL,
    [PlanId] uniqueidentifier NOT NULL,
    [Status] nvarchar(max) NOT NULL,
    [ExternalCustomerId] nvarchar(max) NOT NULL,
    [ExternalSubscriptionId] nvarchar(max) NOT NULL,
    [CurrentPeriodStartUtc] datetime2 NOT NULL,
    [CurrentPeriodEndUtc] datetime2 NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Subscriptions] PRIMARY KEY ([Id])
);

CREATE TABLE [Tags] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Tags] PRIMARY KEY ([Id])
);

CREATE TABLE [Teams] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Teams] PRIMARY KEY ([Id])
);

CREATE TABLE [TenantEntitlements] (
    [TenantId] uniqueidentifier NOT NULL,
    [TenantSlug] nvarchar(128) NOT NULL,
    [TenantStatus] nvarchar(32) NOT NULL,
    [LicensePlan] nvarchar(64) NOT NULL,
    [LicenseStatus] nvarchar(32) NOT NULL,
    [MaxUsers] int NOT NULL,
    [StartsAtUtc] datetime2 NOT NULL,
    [ExpiresAtUtc] datetime2 NULL,
    [Version] bigint NOT NULL,
    [ModulesJson] nvarchar(max) NOT NULL,
    [LimitsJson] nvarchar(max) NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TenantEntitlements] PRIMARY KEY ([TenantId])
);

CREATE TABLE [TenantIndustryPacks] (
    [Id] uniqueidentifier NOT NULL,
    [IndustryPackId] uniqueidentifier NOT NULL,
    [Enabled] bit NOT NULL,
    [OverridesJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TenantIndustryPacks] PRIMARY KEY ([Id])
);

CREATE TABLE [Tenants] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Slug] nvarchar(450) NOT NULL,
    [IsActive] bit NOT NULL,
    [PlanCode] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
);

CREATE TABLE [TenantSettings] (
    [Id] uniqueidentifier NOT NULL,
    [Key] nvarchar(max) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TenantSettings] PRIMARY KEY ([Id])
);

CREATE TABLE [TicketEvents] (
    [Id] uniqueidentifier NOT NULL,
    [TicketId] uniqueidentifier NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [DataJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_TicketEvents] PRIMARY KEY ([Id])
);

CREATE TABLE [Tickets] (
    [Id] uniqueidentifier NOT NULL,
    [ConversationId] uniqueidentifier NULL,
    [ContactId] uniqueidentifier NULL,
    [Number] nvarchar(max) NOT NULL,
    [Subject] nvarchar(max) NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [Priority] int NOT NULL,
    [AssignedUserId] uniqueidentifier NULL,
    [SlaPolicyId] uniqueidentifier NULL,
    [FirstResponseDueUtc] datetime2 NULL,
    [ResolutionDueUtc] datetime2 NULL,
    [ResolvedAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Tickets] PRIMARY KEY ([Id])
);

CREATE TABLE [UsageRecords] (
    [Id] uniqueidentifier NOT NULL,
    [Meter] nvarchar(max) NOT NULL,
    [Quantity] decimal(18,2) NOT NULL,
    [RecordedAtUtc] datetime2 NOT NULL,
    [ReferenceId] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UsageRecords] PRIMARY KEY ([Id])
);

CREATE TABLE [UserRoles] (
    [Id] uniqueidentifier NOT NULL,
    [UserId] uniqueidentifier NOT NULL,
    [RoleId] uniqueidentifier NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_UserRoles] PRIMARY KEY ([Id])
);

CREATE TABLE [WebhookDeliverys] (
    [Id] uniqueidentifier NOT NULL,
    [SubscriptionId] uniqueidentifier NOT NULL,
    [EventName] nvarchar(max) NOT NULL,
    [StatusCode] int NOT NULL,
    [Success] bit NOT NULL,
    [ResponseBody] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WebhookDeliverys] PRIMARY KEY ([Id])
);

CREATE TABLE [WebhookSubscriptions] (
    [Id] uniqueidentifier NOT NULL,
    [EventName] nvarchar(max) NOT NULL,
    [TargetUrl] nvarchar(max) NOT NULL,
    [Secret] nvarchar(max) NOT NULL,
    [Active] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WebhookSubscriptions] PRIMARY KEY ([Id])
);

CREATE TABLE [WorkflowEdges] (
    [Id] uniqueidentifier NOT NULL,
    [FlowId] uniqueidentifier NOT NULL,
    [FromNodeKey] nvarchar(max) NOT NULL,
    [ToNodeKey] nvarchar(max) NOT NULL,
    [ConditionJson] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowEdges] PRIMARY KEY ([Id])
);

CREATE TABLE [WorkflowNodes] (
    [Id] uniqueidentifier NOT NULL,
    [FlowId] uniqueidentifier NOT NULL,
    [NodeKey] nvarchar(max) NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [ConfigJson] nvarchar(max) NOT NULL,
    [X] int NOT NULL,
    [Y] int NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_WorkflowNodes] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_AppUsers_TenantId_Email] ON [AppUsers] ([TenantId], [Email]);

CREATE INDEX [IX_InboxMessages_ProcessedAtUtc] ON [InboxMessages] ([ProcessedAtUtc]);

CREATE INDEX [IX_KnowledgeChunks_TenantId_DocumentId_ChunkIndex] ON [KnowledgeChunks] ([TenantId], [DocumentId], [ChunkIndex]);

CREATE INDEX [IX_Leads_TenantId_Score] ON [Leads] ([TenantId], [Score]);

CREATE INDEX [IX_Messages_TenantId_ConversationId_CreatedAtUtc] ON [Messages] ([TenantId], [ConversationId], [CreatedAtUtc]);

CREATE INDEX [IX_TenantEntitlements_TenantSlug] ON [TenantEntitlements] ([TenantSlug]);

CREATE UNIQUE INDEX [IX_Tenants_Slug] ON [Tenants] ([Slug]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260827111451_InitialBusiness', N'9.0.18');

ALTER TABLE [Opportunitys] ADD [ClosedAtUtc] datetime2 NULL;

ALTER TABLE [Opportunitys] ADD [LossReason] nvarchar(1000) NOT NULL DEFAULT N'';

DECLARE @var sysname;
SELECT @var = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PipelineStages]') AND [c].[name] = N'Probability');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [PipelineStages] DROP CONSTRAINT [' + @var + '];');
ALTER TABLE [PipelineStages] ALTER COLUMN [Probability] decimal(5,2) NOT NULL;

DECLARE @var1 sysname;
SELECT @var1 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Companys]') AND [c].[name] = N'Domain');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Companys] DROP CONSTRAINT [' + @var1 + '];');
ALTER TABLE [Companys] ALTER COLUMN [Domain] nvarchar(253) NOT NULL;

DECLARE @var2 sysname;
SELECT @var2 = [d].[name]
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Contacts]') AND [c].[name] = N'Email');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Contacts] DROP CONSTRAINT [' + @var2 + '];');
ALTER TABLE [Contacts] ALTER COLUMN [Email] nvarchar(320) NOT NULL;

CREATE INDEX [IX_Companys_TenantId_Domain] ON [Companys] ([TenantId], [Domain]);

CREATE INDEX [IX_Contacts_TenantId_Email] ON [Contacts] ([TenantId], [Email]);

CREATE INDEX [IX_Opportunitys_TenantId_Status_PipelineStageId] ON [Opportunitys] ([TenantId], [Status], [PipelineStageId]);

CREATE INDEX [IX_Pipelines_TenantId_IsDefault] ON [Pipelines] ([TenantId], [IsDefault]);

CREATE UNIQUE INDEX [IX_PipelineStages_TenantId_PipelineId_SortOrder] ON [PipelineStages] ([TenantId], [PipelineId], [SortOrder]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260828195500_AlignCrmLifecycle', N'9.0.18');

IF COL_LENGTH('IcpProfiles', 'Active') IS NULL
    ALTER TABLE [IcpProfiles] ADD [Active] bit NOT NULL CONSTRAINT [DF_IcpProfiles_Active] DEFAULT CAST(1 AS bit);
IF COL_LENGTH('IcpProfiles', 'CountriesCsv') IS NULL
    ALTER TABLE [IcpProfiles] ADD [CountriesCsv] nvarchar(500) NOT NULL CONSTRAINT [DF_IcpProfiles_CountriesCsv] DEFAULT N'';
IF COL_LENGTH('IcpProfiles', 'Industry') IS NULL
    ALTER TABLE [IcpProfiles] ADD [Industry] nvarchar(120) NOT NULL CONSTRAINT [DF_IcpProfiles_Industry] DEFAULT N'';
IF COL_LENGTH('IcpProfiles', 'IntentKeywordsCsv') IS NULL
    ALTER TABLE [IcpProfiles] ADD [IntentKeywordsCsv] nvarchar(1000) NOT NULL CONSTRAINT [DF_IcpProfiles_IntentKeywordsCsv] DEFAULT N'';
IF COL_LENGTH('IcpProfiles', 'LastDiscoveryAtUtc') IS NULL
    ALTER TABLE [IcpProfiles] ADD [LastDiscoveryAtUtc] datetime2 NULL;
IF COL_LENGTH('IcpProfiles', 'MaximumEmployees') IS NULL
    ALTER TABLE [IcpProfiles] ADD [MaximumEmployees] int NULL;
IF COL_LENGTH('IcpProfiles', 'MinimumEmployees') IS NULL
    ALTER TABLE [IcpProfiles] ADD [MinimumEmployees] int NULL;

IF COL_LENGTH('IcpProfiles', 'Name') IS NOT NULL AND COL_LENGTH('IcpProfiles', 'Name') > 400
    ALTER TABLE [IcpProfiles] ALTER COLUMN [Name] nvarchar(200) NOT NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_IcpProfiles_TenantId_Active'
      AND object_id = OBJECT_ID(N'[IcpProfiles]'))
    CREATE INDEX [IX_IcpProfiles_TenantId_Active] ON [IcpProfiles] ([TenantId], [Active]);

CREATE TABLE [Campaigns] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [TargetListId] uniqueidentifier NOT NULL,
    [Name] nvarchar(max) NOT NULL,
    [Goal] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [SenderName] nvarchar(max) NOT NULL,
    [SenderEmail] nvarchar(max) NOT NULL,
    [StartsAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Campaigns] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_Campaigns_TenantId_Status_StartsAtUtc] ON [Campaigns] ([TenantId], [Status], [StartsAtUtc]);

CREATE TABLE [CampaignRecipients] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [ProspectId] uniqueidentifier NOT NULL,
    [CurrentStep] int NOT NULL,
    [Status] nvarchar(40) NOT NULL,
    [NextRunAtUtc] datetime2 NULL,
    [RepliedAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CampaignRecipients] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_CampaignRecipients_Status_NextRunAtUtc] ON [CampaignRecipients] ([Status], [NextRunAtUtc]);

CREATE UNIQUE INDEX [IX_CampaignRecipients_TenantId_CampaignId_ProspectId] ON [CampaignRecipients] ([TenantId], [CampaignId], [ProspectId]) WHERE [TenantId] IS NOT NULL AND [CampaignId] IS NOT NULL AND [ProspectId] IS NOT NULL;

CREATE TABLE [CampaignSteps] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [StepNumber] int NOT NULL,
    [DelayHours] int NOT NULL,
    [Channel] nvarchar(max) NOT NULL,
    [SubjectTemplate] nvarchar(max) NOT NULL,
    [BodyTemplate] nvarchar(max) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_CampaignSteps] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_CampaignSteps_TenantId_CampaignId_StepNumber] ON [CampaignSteps] ([TenantId], [CampaignId], [StepNumber]) WHERE [TenantId] IS NOT NULL AND [CampaignId] IS NOT NULL AND [StepNumber] IS NOT NULL;

CREATE TABLE [Prospects] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CompanyId] uniqueidentifier NULL,
    [ContactId] uniqueidentifier NULL,
    [CompanyName] nvarchar(250) NOT NULL,
    [Domain] nvarchar(253) NOT NULL,
    [ContactName] nvarchar(max) NOT NULL,
    [Email] nvarchar(320) NOT NULL,
    [JobTitle] nvarchar(160) NOT NULL,
    [Industry] nvarchar(120) NOT NULL,
    [Country] nvarchar(100) NOT NULL,
    [Source] nvarchar(80) NOT NULL,
    [FitScore] int NOT NULL,
    [IntentScore] int NOT NULL,
    [Status] int NOT NULL,
    [LastEvaluatedAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_Prospects] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_Prospects_TenantId_Domain] ON [Prospects] ([TenantId], [Domain]) WHERE [Domain] <> N'';

CREATE UNIQUE INDEX [IX_Prospects_TenantId_Email] ON [Prospects] ([TenantId], [Email]) WHERE [Email] <> N'';

CREATE INDEX [IX_Prospects_TenantId_Status_FitScore_IntentScore] ON [Prospects] ([TenantId], [Status], [FitScore], [IntentScore]);

CREATE TABLE [ProspectSignals] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [ProspectId] uniqueidentifier NOT NULL,
    [Type] nvarchar(100) NOT NULL,
    [Source] nvarchar(100) NOT NULL,
    [Evidence] nvarchar(max) NOT NULL,
    [Score] int NOT NULL,
    [SourceUrl] nvarchar(2000) NOT NULL,
    [ObservedAtUtc] datetime2 NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProspectSignals] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_ProspectSignals_TenantId_ProspectId_ObservedAtUtc] ON [ProspectSignals] ([TenantId], [ProspectId], [ObservedAtUtc]);

CREATE TABLE [TargetLists] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [IcpProfileId] uniqueidentifier NULL,
    [Description] nvarchar(max) NOT NULL,
    [Dynamic] bit NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TargetLists] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_TargetLists_TenantId_Name] ON [TargetLists] ([TenantId], [Name]);

CREATE TABLE [TargetListMembers] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [TargetListId] uniqueidentifier NOT NULL,
    [ProspectId] uniqueidentifier NOT NULL,
    [AddedAtUtc] datetime2 NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TargetListMembers] PRIMARY KEY ([Id])
);

CREATE UNIQUE INDEX [IX_TargetListMembers_TenantId_TargetListId_ProspectId] ON [TargetListMembers] ([TenantId], [TargetListId], [ProspectId]) WHERE [TenantId] IS NOT NULL AND [TargetListId] IS NOT NULL AND [ProspectId] IS NOT NULL;

CREATE TABLE [OutreachMessages] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [ProspectId] uniqueidentifier NOT NULL,
    [CampaignStepId] uniqueidentifier NOT NULL,
    [Channel] nvarchar(max) NOT NULL,
    [Direction] nvarchar(max) NOT NULL,
    [Subject] nvarchar(max) NOT NULL,
    [Body] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    [ProviderMessageId] nvarchar(max) NOT NULL,
    [SentAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_OutreachMessages] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_OutreachMessages_TenantId_CampaignId_ProspectId] ON [OutreachMessages] ([TenantId], [CampaignId], [ProspectId]);

CREATE TABLE [ProspectReplies] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [CampaignId] uniqueidentifier NOT NULL,
    [ProspectId] uniqueidentifier NOT NULL,
    [OutreachMessageId] uniqueidentifier NULL,
    [Body] nvarchar(max) NOT NULL,
    [Classification] nvarchar(max) NOT NULL,
    [SentimentScore] int NOT NULL,
    [RequiresHuman] bit NOT NULL,
    [ReceivedAtUtc] datetime2 NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_ProspectReplies] PRIMARY KEY ([Id])
);

CREATE INDEX [IX_ProspectReplies_TenantId_CampaignId_ReceivedAtUtc] ON [ProspectReplies] ([TenantId], [CampaignId], [ReceivedAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260829090000_AddAcquisitionCampaignEngine', N'9.0.18');

ALTER TABLE [Prospects] ADD [Priority] nvarchar(20) NOT NULL DEFAULT N'';

ALTER TABLE [Prospects] ADD [ContactReadiness] nvarchar(80) NOT NULL DEFAULT N'';

ALTER TABLE [Prospects] ADD [SuggestedBuyer] nvarchar(200) NOT NULL DEFAULT N'';

ALTER TABLE [Prospects] ADD [SizeBand] nvarchar(80) NOT NULL DEFAULT N'';

ALTER TABLE [Prospects] ADD [PainHypothesis] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [Prospects] ADD [Offer] nvarchar(500) NOT NULL DEFAULT N'';

ALTER TABLE [Prospects] ADD [SourceUrl] nvarchar(2000) NOT NULL DEFAULT N'';

ALTER TABLE [Prospects] ADD [VerificationStatus] nvarchar(500) NOT NULL DEFAULT N'';

ALTER TABLE [Prospects] ADD [OutreachStatus] nvarchar(80) NOT NULL DEFAULT N'';

ALTER TABLE [Prospects] ADD [DatasetOrigin] nvarchar(200) NOT NULL DEFAULT N'';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260902103000_AddProspectResearchMetadata', N'9.0.18');

ALTER TABLE [UsageRecords] ADD [Metric] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [UsageRecords] ADD [Value] bigint NOT NULL DEFAULT CAST(0 AS bigint);

ALTER TABLE [Notifications] ADD [Message] nvarchar(max) NOT NULL DEFAULT N'';

ALTER TABLE [Notifications] ADD [Type] nvarchar(max) NOT NULL DEFAULT N'';

CREATE TABLE [AutonomousAcquisitionAgentMemories] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AgentId] uniqueidentifier NOT NULL,
    [Key] nvarchar(256) NOT NULL,
    [Value] nvarchar(max) NOT NULL,
    [Category] nvarchar(64) NOT NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_AutonomousAcquisitionAgentMemories] PRIMARY KEY ([Id])
);

CREATE TABLE [AutonomousAcquisitionAgentRuns] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [AgentId] uniqueidentifier NOT NULL,
    [Status] int NOT NULL,
    [IsManual] bit NOT NULL,
    [Query] nvarchar(2000) NULL,
    [ScheduledAtUtc] datetime2 NOT NULL,
    [StartedAtUtc] datetime2 NULL,
    [CompletedAtUtc] datetime2 NULL,
    [DiscoveredCount] int NOT NULL,
    [QualifiedCount] int NOT NULL,
    [HighScoreCount] int NOT NULL,
    [EmailsQueuedCount] int NOT NULL,
    [EmailsSentCount] int NOT NULL,
    [Error] nvarchar(4000) NULL,
    CONSTRAINT [PK_AutonomousAcquisitionAgentRuns] PRIMARY KEY ([Id])
);

CREATE TABLE [AutonomousAcquisitionAgents] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [TemplateCode] nvarchar(100) NOT NULL,
    [Industry] nvarchar(200) NOT NULL,
    [Region] nvarchar(100) NOT NULL,
    [CountriesJson] nvarchar(max) NOT NULL,
    [IcpJson] nvarchar(max) NOT NULL,
    [MinimumScore] int NOT NULL,
    [DailyDiscoveryLimit] int NOT NULL,
    [DailyEmailLimit] int NOT NULL,
    [RunTimeUtc] time NOT NULL,
    [Status] int NOT NULL,
    [LastRunAtUtc] datetime2 NULL,
    [CreatedAtUtc] datetime2 NOT NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_AutonomousAcquisitionAgents] PRIMARY KEY ([Id])
);

CREATE TABLE [TenantModuleProvisionings] (
    [TenantId] uniqueidentifier NOT NULL,
    [ModuleCode] nvarchar(128) NOT NULL,
    [Status] nvarchar(32) NOT NULL,
    [AttemptCount] int NOT NULL,
    [LastError] nvarchar(4000) NULL,
    [LastAttemptAtUtc] datetime2 NULL,
    [CompletedAtUtc] datetime2 NULL,
    [NextRetryAtUtc] datetime2 NULL,
    [UpdatedAtUtc] datetime2 NOT NULL,
    CONSTRAINT [PK_TenantModuleProvisionings] PRIMARY KEY ([TenantId], [ModuleCode])
);

CREATE INDEX [IX_AutonomousAcquisitionAgents_TenantId_Status] ON [AutonomousAcquisitionAgents] ([TenantId], [Status]);

CREATE INDEX [IX_AutonomousAcquisitionAgentRuns_TenantId_AgentId_ScheduledAtUtc] ON [AutonomousAcquisitionAgentRuns] ([TenantId], [AgentId], [ScheduledAtUtc]);

CREATE UNIQUE INDEX [IX_AutonomousAcquisitionAgentMemories_TenantId_AgentId_Key] ON [AutonomousAcquisitionAgentMemories] ([TenantId], [AgentId], [Key]);

CREATE INDEX [IX_TenantModuleProvisionings_Status_NextRetryAtUtc] ON [TenantModuleProvisionings] ([Status], [NextRetryAtUtc]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260905205249_SyncPlatformModelAfterAutonomousAcquisitionAndProvisioning', N'9.0.18');


IF OBJECT_ID(N'[TenantLifecycleEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [TenantLifecycleEvents]
    (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] nvarchar(64) NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [DataJson] nvarchar(8000) NULL,
        [CorrelationId] nvarchar(128) NULL,
        [Source] nvarchar(128) NOT NULL,
        [ActorId] nvarchar(256) NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TenantLifecycleEvents] PRIMARY KEY ([Id])
    );

    CREATE INDEX [IX_TenantLifecycleEvents_TenantId_OccurredAtUtc]
        ON [TenantLifecycleEvents] ([TenantId], [OccurredAtUtc]);

    CREATE INDEX [IX_TenantLifecycleEvents_CorrelationId]
        ON [TenantLifecycleEvents] ([CorrelationId]);
END
ELSE
BEGIN
    IF COL_LENGTH(N'[TenantLifecycleEvents]', N'DataJson') IS NOT NULL
        AND EXISTS (
            SELECT 1
            FROM sys.columns c
            JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID(N'[TenantLifecycleEvents]')
              AND c.name = N'DataJson'
              AND t.name IN (N'nvarchar', N'varchar')
              AND c.max_length <> -1)
    BEGIN
        ALTER TABLE [TenantLifecycleEvents]
            ALTER COLUMN [DataJson] nvarchar(8000) NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'[TenantLifecycleEvents]')
          AND name = N'IX_TenantLifecycleEvents_TenantId_OccurredAtUtc')
    BEGIN
        CREATE INDEX [IX_TenantLifecycleEvents_TenantId_OccurredAtUtc]
            ON [TenantLifecycleEvents] ([TenantId], [OccurredAtUtc]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'[TenantLifecycleEvents]')
          AND name = N'IX_TenantLifecycleEvents_CorrelationId')
    BEGIN
        CREATE INDEX [IX_TenantLifecycleEvents_CorrelationId]
            ON [TenantLifecycleEvents] ([CorrelationId]);
    END;
END

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260907003134_AddTenantLifecycleEvents', N'9.0.18');

COMMIT;
GO

