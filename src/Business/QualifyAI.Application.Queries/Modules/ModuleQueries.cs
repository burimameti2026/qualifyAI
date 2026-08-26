using MediatR;
using QualifyAI.Domain;

namespace QualifyAI.Application.Queries.Modules;

public sealed record ListKnowledgeBasesQuery(Guid TenantId) : IRequest<IReadOnlyList<KnowledgeBase>>;
public sealed record ListKnowledgeDocumentsQuery(Guid TenantId) : IRequest<IReadOnlyList<KnowledgeDocument>>;
public sealed record ListKnowledgeGapsQuery(Guid TenantId) : IRequest<IReadOnlyList<KnowledgeGap>>;
public sealed record ListAiAgentsQuery(Guid TenantId) : IRequest<IReadOnlyList<AiAgent>>;
public sealed record GetWorkflowDesignerQuery(Guid TenantId, Guid FlowId) : IRequest<WorkflowDesignerDto>;
public sealed record ListWorkflowsQuery(Guid TenantId) : IRequest<IReadOnlyList<QualificationFlow>>;
public sealed record GetSalesPipelinesQuery(Guid TenantId) : IRequest<SalesPipelinesDto>;
public sealed record ListMeetingsQuery(Guid TenantId) : IRequest<IReadOnlyList<MeetingBooking>>;
public sealed record ListIntegrationsQuery(Guid TenantId) : IRequest<IReadOnlyList<IntegrationConnection>>;
public sealed record ListAutomationsQuery(Guid TenantId) : IRequest<IReadOnlyList<AutomationRule>>;
public sealed record ListEvaluationDatasetsQuery(Guid TenantId) : IRequest<IReadOnlyList<EvaluationDataset>>;
public sealed record GetAnalyticsOverviewQuery(Guid TenantId) : IRequest<AnalyticsOverviewDto>;
public sealed record ListBillingPlansQuery() : IRequest<IReadOnlyList<Plan>>;
public sealed record GetBillingUsageQuery(Guid TenantId) : IRequest<IReadOnlyList<UsageMeterDto>>;
public sealed record ListSsoConfigurationsQuery(Guid TenantId) : IRequest<IReadOnlyList<SsoConfiguration>>;
public sealed record ListRetentionPoliciesQuery(Guid TenantId) : IRequest<IReadOnlyList<DataRetentionPolicy>>;
public sealed record GetBrandingQuery(Guid TenantId) : IRequest<BrandingProfile?>;
public sealed record ListCustomDomainsQuery(Guid TenantId) : IRequest<IReadOnlyList<CustomDomain>>;
public sealed record ListIndustryPacksQuery() : IRequest<IReadOnlyList<IndustryPack>>;
public sealed record ListAuditLogsQuery(Guid TenantId, int Take = 500) : IRequest<IReadOnlyList<AuditLog>>;
public sealed record ListSalesTasksQuery(Guid TenantId, int Take = 500) : IRequest<IReadOnlyList<CrmTask>>;
public sealed record ListRevenueAttributionQuery(Guid TenantId, int Take = 500) : IRequest<IReadOnlyList<RevenueAttribution>>;

public sealed record WorkflowDesignerDto(IReadOnlyList<WorkflowNode> Nodes, IReadOnlyList<WorkflowEdge> Edges);
public sealed record SalesPipelinesDto(IReadOnlyList<Pipeline> Pipelines, IReadOnlyList<PipelineStage> Stages);
public sealed record AnalyticsOverviewDto(int Qualified, int Hot, decimal Pipeline, decimal Won, int AiConversations, int Tickets);
public sealed record UsageMeterDto(string Meter, decimal Quantity);
