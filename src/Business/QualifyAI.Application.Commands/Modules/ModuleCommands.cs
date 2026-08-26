using MediatR;
using QualifyAI.Domain;

namespace QualifyAI.Application.Commands.Modules;

public sealed record CreateCompanyCommand(Guid TenantId, Company Company) : IRequest<Company>;
public sealed record UpdateContactCommand(Guid TenantId, Guid Id, Contact Contact) : IRequest<Contact?>;
public sealed record UpdateOpportunityCommand(Guid TenantId, Guid Id, Opportunity Opportunity) : IRequest<Opportunity?>;
public sealed record MoveOpportunityStageCommand(Guid TenantId, Guid Id, Guid StageId) : IRequest<Opportunity?>;
public sealed record TakeoverConversationCommand(Guid TenantId, Guid ConversationId, Guid? UserId) : IRequest<Conversation?>;
public sealed record AddConversationMessageCommand(Guid TenantId, Guid ConversationId, Guid? UserId, string Text, string SenderType) : IRequest<Message?>;
public sealed record AddConversationNoteCommand(Guid TenantId, Guid ConversationId, Guid UserId, string Text) : IRequest<ConversationNote>;
public sealed record UpdateConversationCommand(Guid TenantId, Guid ConversationId, string Status, bool? AiEnabled) : IRequest<Conversation?>;
public sealed record UpdateTicketCommand(Guid TenantId, Guid Id, Ticket Ticket) : IRequest<Ticket?>;
public sealed record CreateKnowledgeDocumentCommand(Guid TenantId, KnowledgeDocument Document) : IRequest<KnowledgeDocument>;
public sealed record UpdateKnowledgeDocumentCommand(Guid TenantId, Guid Id, KnowledgeDocument Document) : IRequest<KnowledgeDocument?>;
public sealed record ReindexKnowledgeDocumentCommand(Guid TenantId, Guid Id) : IRequest<ReindexResult?>;
public sealed record UpdateKnowledgeGapCommand(Guid TenantId, Guid Id, KnowledgeGap Gap) : IRequest<KnowledgeGap?>;
public sealed record CreateAiAgentCommand(Guid TenantId, AiAgent Agent) : IRequest<AiAgent>;
public sealed record UpdateAiAgentCommand(Guid TenantId, Guid Id, AiAgent Agent) : IRequest<AiAgent?>;
public sealed record SaveWorkflowDesignerCommand(Guid TenantId, Guid FlowId, IReadOnlyList<WorkflowNode> Nodes, IReadOnlyList<WorkflowEdge> Edges) : IRequest<WorkflowSaveResult>;
public sealed record CreateAutomationCommand(Guid TenantId, AutomationRule Rule) : IRequest<AutomationRule>;
public sealed record UpdateAutomationCommand(Guid TenantId, Guid Id, AutomationRule Rule) : IRequest<AutomationRule?>;
public sealed record RunAutomationCommand(Guid TenantId, Guid Id) : IRequest<AutomationRun?>;
public sealed record CreateIntegrationCommand(Guid TenantId, IntegrationConnection Connection) : IRequest<IntegrationConnection>;
public sealed record UpdateIntegrationCommand(Guid TenantId, Guid Id, IntegrationConnection Connection) : IRequest<IntegrationConnection?>;
public sealed record TestIntegrationCommand(Guid TenantId, Guid Id) : IRequest<IntegrationTestResult?>;
public sealed record UpdateBrandingCommand(Guid TenantId, BrandingProfile Branding) : IRequest<BrandingProfile>;
public sealed record InstallIndustryPackCommand(Guid TenantId, Guid Id) : IRequest<bool>;
public sealed record CreateMeetingCommand(Guid TenantId, MeetingBooking Meeting) : IRequest<MeetingBooking>;
public sealed record UpdateSalesTaskCommand(Guid TenantId, Guid Id, CrmTask Task) : IRequest<CrmTask?>;

public sealed record ReindexResult(Guid DocumentId, int Chunks, string Status);
public sealed record WorkflowSaveResult(int Nodes, int Edges);
public sealed record IntegrationTestResult(bool Success, string Provider, DateTime CheckedAtUtc);
