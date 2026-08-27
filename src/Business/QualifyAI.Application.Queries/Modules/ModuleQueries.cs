using MediatR;
using QualifyAI.BuildingBlocks.Application.Security;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.Domain;

namespace QualifyAI.Application.Queries.Modules;

[AccessControl(QualifyAiPermissions.KnowledgeRead, QualifyAiModules.Knowledge)]
public sealed record ListKnowledgeBasesQuery(Guid TenantId) : IRequest<IReadOnlyList<KnowledgeBase>>;
[AccessControl(QualifyAiPermissions.KnowledgeRead, QualifyAiModules.Knowledge)]
public sealed record ListKnowledgeDocumentsQuery(Guid TenantId) : IRequest<IReadOnlyList<KnowledgeDocument>>;
[AccessControl(QualifyAiPermissions.KnowledgeRead, QualifyAiModules.Knowledge)]
public sealed record ListKnowledgeGapsQuery(Guid TenantId) : IRequest<IReadOnlyList<KnowledgeGap>>;
[AccessControl(QualifyAiPermissions.AgentsRead, QualifyAiModules.Ai)]
public sealed record ListAiAgentsQuery(Guid TenantId) : IRequest<IReadOnlyList<AiAgent>>;
[AccessControl(QualifyAiPermissions.AutomationRead, QualifyAiModules.Automation)]
public sealed record GetWorkflowDesignerQuery(Guid TenantId, Guid FlowId) : IRequest<WorkflowDesignerDto>;
[AccessControl(QualifyAiPermissions.AutomationRead, QualifyAiModules.Automation)]
public sealed record ListWorkflowsQuery(Guid TenantId) : IRequest<IReadOnlyList<QualificationFlow>>;
[AccessControl(QualifyAiPermissions.CrmRead, QualifyAiModules.Crm)]
public sealed record GetSalesPipelinesQuery(Guid TenantId) : IRequest<SalesPipelinesDto>;
[AccessControl(QualifyAiPermissions.CrmRead, QualifyAiModules.Crm)]
public sealed record ListMeetingsQuery(Guid TenantId) : IRequest<IReadOnlyList<MeetingBooking>>;
[AccessControl(QualifyAiPermissions.IntegrationsRead, QualifyAiModules.Integrations)]
public sealed record ListIntegrationsQuery(Guid TenantId) : IRequest<IReadOnlyList<IntegrationConnection>>;
[AccessControl(QualifyAiPermissions.AutomationRead, QualifyAiModules.Automation)]
public sealed record ListAutomationsQuery(Guid TenantId) : IRequest<IReadOnlyList<AutomationRule>>;
[AccessControl(QualifyAiPermissions.AgentsRead, QualifyAiModules.Ai)]
public sealed record ListEvaluationDatasetsQuery(Guid TenantId) : IRequest<IReadOnlyList<EvaluationDataset>>;
[AccessControl(QualifyAiPermissions.AnalyticsRead, QualifyAiModules.Analytics)]
public sealed record GetAnalyticsOverviewQuery(Guid TenantId) : IRequest<AnalyticsOverviewDto>;
[AccessControl(QualifyAiPermissions.BillingRead, QualifyAiModules.Billing)]
public sealed record ListBillingPlansQuery(Guid TenantId) : IRequest<IReadOnlyList<Plan>>;
[AccessControl(QualifyAiPermissions.BillingRead, QualifyAiModules.Billing)]
public sealed record GetBillingUsageQuery(Guid TenantId) : IRequest<IReadOnlyList<UsageMeterDto>>;
[AccessControl(QualifyAiPermissions.SettingsManage, QualifyAiModules.Settings)]
public sealed record ListSsoConfigurationsQuery(Guid TenantId) : IRequest<IReadOnlyList<SsoConfiguration>>;
[AccessControl(QualifyAiPermissions.SettingsManage, QualifyAiModules.Settings)]
public sealed record ListRetentionPoliciesQuery(Guid TenantId) : IRequest<IReadOnlyList<DataRetentionPolicy>>;
[AccessControl(QualifyAiPermissions.SettingsManage, QualifyAiModules.Settings)]
public sealed record GetBrandingQuery(Guid TenantId) : IRequest<BrandingProfile?>;
[AccessControl(QualifyAiPermissions.SettingsManage, QualifyAiModules.Settings)]
public sealed record ListCustomDomainsQuery(Guid TenantId) : IRequest<IReadOnlyList<CustomDomain>>;
[AccessControl(QualifyAiPermissions.SettingsManage, QualifyAiModules.Settings)]
public sealed record ListIndustryPacksQuery(Guid TenantId) : IRequest<IReadOnlyList<IndustryPack>>;
[AccessControl(QualifyAiPermissions.AuditRead, QualifyAiModules.Settings)]
public sealed record ListAuditLogsQuery(Guid TenantId, int Take = 500) : IRequest<IReadOnlyList<AuditLog>>;
[AccessControl(QualifyAiPermissions.CrmRead, QualifyAiModules.Crm)]
public sealed record ListSalesTasksQuery(Guid TenantId, int Take = 500) : IRequest<IReadOnlyList<CrmTask>>;
[AccessControl(QualifyAiPermissions.AnalyticsRead, QualifyAiModules.Analytics)]
public sealed record ListRevenueAttributionQuery(Guid TenantId, int Take = 500) : IRequest<IReadOnlyList<RevenueAttribution>>;

public sealed record WorkflowDesignerDto(IReadOnlyList<WorkflowNode> Nodes, IReadOnlyList<WorkflowEdge> Edges);
public sealed record SalesPipelinesDto(IReadOnlyList<Pipeline> Pipelines, IReadOnlyList<PipelineStage> Stages);
public sealed record AnalyticsOverviewDto(int Qualified, int Hot, decimal Pipeline, decimal Won, int AiConversations, int Tickets);
public sealed record UsageMeterDto(string Meter, decimal Quantity);
