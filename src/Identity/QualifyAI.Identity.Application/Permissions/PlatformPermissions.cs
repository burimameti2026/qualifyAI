using QualifyAI.BuildingBlocks.Security.Access;

namespace QualifyAI.Identity.Application.Permissions;

public static class PlatformPermissions
{
    public const string SystemAdmin = QualifyAiPermissions.SystemAdmin;
    public const string UsersRead = QualifyAiPermissions.UsersRead;
    public const string UsersManage = QualifyAiPermissions.UsersManage;
    public const string CrmRead = QualifyAiPermissions.CrmRead;
    public const string CrmManage = QualifyAiPermissions.CrmManage;
    public const string ConversationsRead = QualifyAiPermissions.ConversationsRead;
    public const string ConversationsManage = QualifyAiPermissions.ConversationsManage;
    public const string TicketsRead = QualifyAiPermissions.TicketsRead;
    public const string TicketsManage = QualifyAiPermissions.TicketsManage;
    public const string KnowledgeRead = QualifyAiPermissions.KnowledgeRead;
    public const string KnowledgeManage = QualifyAiPermissions.KnowledgeManage;
    public const string AgentsRead = QualifyAiPermissions.AgentsRead;
    public const string AgentsManage = QualifyAiPermissions.AgentsManage;
    public const string AutomationRead = QualifyAiPermissions.AutomationRead;
    public const string AutomationManage = QualifyAiPermissions.AutomationManage;
    public const string IntegrationsRead = QualifyAiPermissions.IntegrationsRead;
    public const string IntegrationsManage = QualifyAiPermissions.IntegrationsManage;
    public const string AnalyticsRead = QualifyAiPermissions.AnalyticsRead;
    public const string BillingRead = QualifyAiPermissions.BillingRead;
    public const string BillingManage = QualifyAiPermissions.BillingManage;
    public const string AuditRead = QualifyAiPermissions.AuditRead;
    public const string SettingsManage = QualifyAiPermissions.SettingsManage;

    public static readonly string[] All = QualifyAiPermissions.All;
}
