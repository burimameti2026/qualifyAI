using MediatR;
using QualifyAI.BuildingBlocks.Application.Security;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.Domain;

namespace QualifyAI.Application.Commands.Modules;

[AccessControl(QualifyAiPermissions.CrmManage, QualifyAiModules.Crm)]
public sealed record CreateCompanyCommand(Guid TenantId, Company Company) : IRequest<Company>;
[AccessControl(QualifyAiPermissions.CrmManage, QualifyAiModules.Crm)]
public sealed record UpdateContactCommand(Guid TenantId, Guid Id, Contact Contact) : IRequest<Contact?>;
[AccessControl(QualifyAiPermissions.CrmManage, QualifyAiModules.Crm)]
public sealed record UpdateOpportunityCommand(Guid TenantId, Guid Id, Opportunity Opportunity) : IRequest<Opportunity?>;
[AccessControl(QualifyAiPermissions.CrmManage, QualifyAiModules.Crm)]
public sealed record MoveOpportunityStageCommand(Guid TenantId, Guid Id, Guid StageId) : IRequest<Opportunity?>;
[AccessControl(QualifyAiPermissions.ConversationsManage, QualifyAiModules.Inbox)]
public sealed record TakeoverConversationCommand(Guid TenantId, Guid ConversationId, Guid? UserId) : IRequest<Conversation?>;
[AccessControl(QualifyAiPermissions.ConversationsManage, QualifyAiModules.Inbox)]
public sealed record AddConversationMessageCommand(Guid TenantId, Guid ConversationId, Guid? UserId, string Text, string SenderType) : IRequest<Message?>;
[AccessControl(QualifyAiPermissions.ConversationsManage, QualifyAiModules.Inbox)]
public sealed record AddConversationNoteCommand(Guid TenantId, Guid ConversationId, Guid UserId, string Text) : IRequest<ConversationNote>;
[AccessControl(QualifyAiPermissions.ConversationsManage, QualifyAiModules.Inbox)]
public sealed record UpdateConversationCommand(Guid TenantId, Guid ConversationId, string Status, bool? AiEnabled) : IRequest<Conversation?>;
[AccessControl(QualifyAiPermissions.TicketsManage, QualifyAiModules.Ticketing)]
public sealed record UpdateTicketCommand(Guid TenantId, Guid Id, Ticket Ticket) : IRequest<Ticket?>;
[AccessControl(QualifyAiPermissions.KnowledgeManage, QualifyAiModules.Knowledge)]
public sealed record CreateKnowledgeDocumentCommand(Guid TenantId, KnowledgeDocument Document) : IRequest<KnowledgeDocument>;
[AccessControl(QualifyAiPermissions.KnowledgeManage, QualifyAiModules.Knowledge)]
public sealed record UpdateKnowledgeDocumentCommand(Guid TenantId, Guid Id, KnowledgeDocument Document) : IRequest<KnowledgeDocument?>;
[AccessControl(QualifyAiPermissions.KnowledgeManage, QualifyAiModules.Knowledge)]
public sealed record ReindexKnowledgeDocumentCommand(Guid TenantId, Guid Id) : IRequest<ReindexResult?>;
[AccessControl(QualifyAiPermissions.KnowledgeManage, QualifyAiModules.Knowledge)]
public sealed record UpdateKnowledgeGapCommand(Guid TenantId, Guid Id, KnowledgeGap Gap) : IRequest<KnowledgeGap?>;
[AccessControl(QualifyAiPermissions.AgentsManage, QualifyAiModules.Ai)]
public sealed record CreateAiAgentCommand(Guid TenantId, AiAgent Agent) : IRequest<AiAgent>;
[AccessControl(QualifyAiPermissions.AgentsManage, QualifyAiModules.Ai)]
public sealed record UpdateAiAgentCommand(Guid TenantId, Guid Id, AiAgent Agent) : IRequest<AiAgent?>;
[AccessControl(QualifyAiPermissions.AutomationManage, QualifyAiModules.Automation)]
public sealed record SaveWorkflowDesignerCommand(Guid TenantId, Guid FlowId, IReadOnlyList<WorkflowNode> Nodes, IReadOnlyList<WorkflowEdge> Edges) : IRequest<WorkflowSaveResult>;
[AccessControl(QualifyAiPermissions.AutomationManage, QualifyAiModules.Automation)]
public sealed record CreateAutomationCommand(Guid TenantId, AutomationRule Rule) : IRequest<AutomationRule>;
[AccessControl(QualifyAiPermissions.AutomationManage, QualifyAiModules.Automation)]
public sealed record UpdateAutomationCommand(Guid TenantId, Guid Id, AutomationRule Rule) : IRequest<AutomationRule?>;
[AccessControl(QualifyAiPermissions.AutomationManage, QualifyAiModules.Automation)]
public sealed record RunAutomationCommand(Guid TenantId, Guid Id) : IRequest<AutomationRun?>;
[AccessControl(QualifyAiPermissions.IntegrationsManage, QualifyAiModules.Integrations)]
public sealed record CreateIntegrationCommand(Guid TenantId, IntegrationConnection Connection) : IRequest<IntegrationConnection>;
[AccessControl(QualifyAiPermissions.IntegrationsManage, QualifyAiModules.Integrations)]
public sealed record UpdateIntegrationCommand(Guid TenantId, Guid Id, IntegrationConnection Connection) : IRequest<IntegrationConnection?>;
[AccessControl(QualifyAiPermissions.IntegrationsManage, QualifyAiModules.Integrations)]
public sealed record TestIntegrationCommand(Guid TenantId, Guid Id) : IRequest<IntegrationTestResult?>;
[AccessControl(QualifyAiPermissions.SettingsManage, QualifyAiModules.Settings)]
public sealed record UpdateBrandingCommand(Guid TenantId, BrandingProfile Branding) : IRequest<BrandingProfile>;
[AccessControl(QualifyAiPermissions.SettingsManage, QualifyAiModules.Settings)]
public sealed record InstallIndustryPackCommand(Guid TenantId, Guid Id) : IRequest<bool>;
[AccessControl(QualifyAiPermissions.CrmManage, QualifyAiModules.Crm)]
public sealed record CreateMeetingCommand(Guid TenantId, MeetingBooking Meeting) : IRequest<MeetingBooking>;
[AccessControl(QualifyAiPermissions.CrmManage, QualifyAiModules.Crm)]
public sealed record UpdateSalesTaskCommand(Guid TenantId, Guid Id, CrmTask Task) : IRequest<CrmTask?>;

public sealed record ReindexResult(Guid DocumentId, int Chunks, string Status);
public sealed record WorkflowSaveResult(int Nodes, int Edges);
public sealed record IntegrationTestResult(bool Success, string Provider, DateTime CheckedAtUtc);
