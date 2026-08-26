namespace QualifyAI.Identity.Application.Permissions;
public static class PlatformPermissions
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
    public const string KnowledgeManage = "knowledge.manage";
    public const string AgentsManage = "agents.manage";
    public const string AutomationManage = "automation.manage";
    public const string BillingManage = "billing.manage";
    public static readonly string[] All =
    [
        SystemAdmin,UsersRead,UsersManage,CrmRead,CrmManage,
        ConversationsRead,ConversationsManage,TicketsRead,TicketsManage,
        KnowledgeManage,AgentsManage,AutomationManage,BillingManage
    ];
}
