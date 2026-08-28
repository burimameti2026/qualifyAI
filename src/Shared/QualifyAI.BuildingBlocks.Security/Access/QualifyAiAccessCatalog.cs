namespace QualifyAI.BuildingBlocks.Security.Access;

public static class QualifyAiPermissions
{
    public const string SystemAdmin = "system.admin";
    public const string UsersRead = "users.read";
    public const string UsersManage = "users.manage";
    public const string CrmRead = "crm.read";
    public const string CrmManage = "crm.manage";
    public const string ConversationsRead = "conversations.read";
    public const string ConversationsManage = "conversations.manage";
    public const string TicketsRead = "tickets.read";
    public const string TicketsManage = "tickets.manage";
    public const string KnowledgeRead = "knowledge.read";
    public const string KnowledgeManage = "knowledge.manage";
    public const string AgentsRead = "agents.read";
    public const string AgentsManage = "agents.manage";
    public const string AutomationRead = "automation.read";
    public const string AutomationManage = "automation.manage";
    public const string IntegrationsRead = "integrations.read";
    public const string IntegrationsManage = "integrations.manage";
    public const string AnalyticsRead = "analytics.read";
    public const string BillingRead = "billing.read";
    public const string BillingManage = "billing.manage";
    public const string AuditRead = "audit.read";
    public const string SettingsManage = "settings.manage";

    public static readonly string[] All =
    [
        SystemAdmin, UsersRead, UsersManage, CrmRead, CrmManage,
        ConversationsRead, ConversationsManage, TicketsRead, TicketsManage,
        KnowledgeRead, KnowledgeManage, AgentsRead, AgentsManage,
        AutomationRead, AutomationManage, IntegrationsRead, IntegrationsManage,
        AnalyticsRead, BillingRead, BillingManage, AuditRead, SettingsManage
    ];
}

public static class QualifyAiModules
{
    public const string Crm = "crm";
    public const string Inbox = "inbox";
    public const string Ticketing = "ticketing";
    public const string Automation = "automation";
    public const string Knowledge = "knowledge";
    public const string Ai = "ai";
    public const string Analytics = "analytics";
    public const string Integrations = "integrations";
    public const string Billing = "billing";
    public const string Settings = "settings";

    public static readonly string[] Enterprise =
    [Crm, Inbox, Ticketing, Automation, Knowledge, Ai, Analytics, Integrations, Billing, Settings];
}
