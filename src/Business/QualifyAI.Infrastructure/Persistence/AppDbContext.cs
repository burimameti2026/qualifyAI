using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Persistence.Configurations;

namespace QualifyAI.Infrastructure;

/// <summary>
/// Single relational persistence boundary for the Business service.
/// Domain areas are organized through model-configuration extensions rather than
/// creating a DbContext for every feature.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    // Platform / tenancy projections
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();

    // CRM / sales
    public DbSet<Company> Companys => Set<Company>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Opportunity> Opportunitys => Set<Opportunity>();
    public DbSet<CrmActivity> CrmActivitys => Set<CrmActivity>();
    public DbSet<CrmTask> CrmTasks => Set<CrmTask>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();
    public DbSet<Pipeline> Pipelines => Set<Pipeline>();
    public DbSet<PipelineStage> PipelineStages => Set<PipelineStage>();
    public DbSet<IcpProfile> IcpProfiles => Set<IcpProfile>();
    public DbSet<LeadScoreExplanation> LeadScoreExplanations => Set<LeadScoreExplanation>();
    public DbSet<MeetingType> MeetingTypes => Set<MeetingType>();
    public DbSet<MeetingBooking> MeetingBookings => Set<MeetingBooking>();
    public DbSet<SalesSequence> SalesSequences => Set<SalesSequence>();
    public DbSet<SequenceStep> SequenceSteps => Set<SequenceStep>();
    public DbSet<SequenceEnrollment> SequenceEnrollments => Set<SequenceEnrollment>();

    // Conversations / support
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<ConversationNote> ConversationNotes => Set<ConversationNote>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Team> Teams => Set<Team>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<SlaPolicy> SlaPolicys => Set<SlaPolicy>();
    public DbSet<TicketEvent> TicketEvents => Set<TicketEvent>();
    public DbSet<CsatResponse> CsatResponses => Set<CsatResponse>();

    // Knowledge / AI / qualification
    public DbSet<KnowledgeBase> KnowledgeBases => Set<KnowledgeBase>();
    public DbSet<KnowledgeSource> KnowledgeSources => Set<KnowledgeSource>();
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();
    public DbSet<KnowledgeGap> KnowledgeGaps => Set<KnowledgeGap>();
    public DbSet<AiAgent> AiAgents => Set<AiAgent>();
    public DbSet<AiAgentVersion> AiAgentVersions => Set<AiAgentVersion>();
    public DbSet<AiToolDefinition> AiToolDefinitions => Set<AiToolDefinition>();
    public DbSet<AiToolExecution> AiToolExecutions => Set<AiToolExecution>();
    public DbSet<PromptVersion> PromptVersions => Set<PromptVersion>();
    public DbSet<QualificationFlow> QualificationFlows => Set<QualificationFlow>();
    public DbSet<WorkflowNode> WorkflowNodes => Set<WorkflowNode>();
    public DbSet<WorkflowEdge> WorkflowEdges => Set<WorkflowEdge>();
    public DbSet<QualificationAnswer> QualificationAnswers => Set<QualificationAnswer>();
    public DbSet<ScoringRule> ScoringRules => Set<ScoringRule>();

    // Integrations / automation / evaluation
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<IntegrationSyncJob> IntegrationSyncJobs => Set<IntegrationSyncJob>();
    public DbSet<FieldMapping> FieldMappings => Set<FieldMapping>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliverys => Set<WebhookDelivery>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<AutomationRun> AutomationRuns => Set<AutomationRun>();
    public DbSet<EvaluationDataset> EvaluationDatasets => Set<EvaluationDataset>();
    public DbSet<EvaluationTestCase> EvaluationTestCases => Set<EvaluationTestCase>();
    public DbSet<EvaluationRun> EvaluationRuns => Set<EvaluationRun>();
    public DbSet<EvaluationResult> EvaluationResults => Set<EvaluationResult>();

    // Analytics / billing / enterprise / white-label
    public DbSet<MetricSnapshot> MetricSnapshots => Set<MetricSnapshot>();
    public DbSet<RevenueAttribution> RevenueAttributions => Set<RevenueAttribution>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<UsageRecord> UsageRecords => Set<UsageRecord>();
    public DbSet<BillingInvoice> BillingInvoices => Set<BillingInvoice>();
    public DbSet<SsoConfiguration> SsoConfigurations => Set<SsoConfiguration>();
    public DbSet<DataRetentionPolicy> DataRetentionPolicys => Set<DataRetentionPolicy>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<PiiRedactionJob> PiiRedactionJobs => Set<PiiRedactionJob>();
    public DbSet<Agency> Agencys => Set<Agency>();
    public DbSet<AgencyClient> AgencyClients => Set<AgencyClient>();
    public DbSet<BrandingProfile> BrandingProfiles => Set<BrandingProfile>();
    public DbSet<CustomDomain> CustomDomains => Set<CustomDomain>();
    public DbSet<IndustryPack> IndustryPacks => Set<IndustryPack>();
    public DbSet<TenantIndustryPack> TenantIndustryPacks => Set<TenantIndustryPack>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureBusinessEntityKeys();
        builder.ConfigurePlatformModel();
        builder.ConfigureCrmModel();
        builder.ConfigureConversationSupportModel();
        builder.ConfigureKnowledgeAiModel();
    }
}
