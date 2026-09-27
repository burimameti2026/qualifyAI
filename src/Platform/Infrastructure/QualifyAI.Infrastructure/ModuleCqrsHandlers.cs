using MediatR;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Modules;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure;

public sealed class BusinessModuleQueryHandlers(AppDbContext db, IKnowledgeAiRepository knowledgeAi, IWorkflowAutomationRepository workflowAutomation) :
    IRequestHandler<ListKnowledgeBasesQuery, IReadOnlyList<KnowledgeBase>>,
    IRequestHandler<ListKnowledgeDocumentsQuery, IReadOnlyList<KnowledgeDocument>>,
    IRequestHandler<ListKnowledgeGapsQuery, IReadOnlyList<KnowledgeGap>>,
    IRequestHandler<ListAiAgentsQuery, IReadOnlyList<AiAgent>>,
    IRequestHandler<GetWorkflowDesignerQuery, WorkflowDesignerDto>,
    IRequestHandler<ListWorkflowsQuery, IReadOnlyList<QualificationFlow>>,
    IRequestHandler<GetSalesPipelinesQuery, SalesPipelinesDto>,
    IRequestHandler<ListMeetingsQuery, IReadOnlyList<MeetingBooking>>,
    IRequestHandler<ListIntegrationsQuery, IReadOnlyList<IntegrationConnection>>,
    IRequestHandler<ListAutomationsQuery, IReadOnlyList<AutomationRule>>,
    IRequestHandler<ListEvaluationDatasetsQuery, IReadOnlyList<EvaluationDataset>>,
    IRequestHandler<GetAnalyticsOverviewQuery, AnalyticsOverviewDto>,
    IRequestHandler<ListBillingPlansQuery, IReadOnlyList<Plan>>,
    IRequestHandler<GetBillingUsageQuery, IReadOnlyList<UsageMeterDto>>,
    IRequestHandler<ListSsoConfigurationsQuery, IReadOnlyList<SsoConfiguration>>,
    IRequestHandler<ListRetentionPoliciesQuery, IReadOnlyList<DataRetentionPolicy>>,
    IRequestHandler<GetBrandingQuery, BrandingProfile?>,
    IRequestHandler<ListCustomDomainsQuery, IReadOnlyList<CustomDomain>>,
    IRequestHandler<ListIndustryPacksQuery, IReadOnlyList<IndustryPack>>,
    IRequestHandler<ListAuditLogsQuery, IReadOnlyList<AuditLog>>,
    IRequestHandler<ListSalesTasksQuery, IReadOnlyList<CrmTask>>,
    IRequestHandler<ListRevenueAttributionQuery, IReadOnlyList<RevenueAttribution>>
{
    public Task<IReadOnlyList<KnowledgeBase>> Handle(ListKnowledgeBasesQuery q, CancellationToken ct) => knowledgeAi.ListKnowledgeBasesAsync(q.TenantId, ct);
    public Task<IReadOnlyList<KnowledgeDocument>> Handle(ListKnowledgeDocumentsQuery q, CancellationToken ct) => knowledgeAi.ListKnowledgeDocumentsAsync(q.TenantId, ct);
    public Task<IReadOnlyList<KnowledgeGap>> Handle(ListKnowledgeGapsQuery q, CancellationToken ct) => knowledgeAi.ListKnowledgeGapsAsync(q.TenantId, ct);
    public Task<IReadOnlyList<AiAgent>> Handle(ListAiAgentsQuery q, CancellationToken ct) => knowledgeAi.ListAiAgentsAsync(q.TenantId, ct);
    public async Task<WorkflowDesignerDto> Handle(GetWorkflowDesignerQuery q, CancellationToken ct) => new(await workflowAutomation.ListWorkflowNodesAsync(q.TenantId, q.FlowId, cancellationToken: ct), await workflowAutomation.ListWorkflowEdgesAsync(q.TenantId, q.FlowId, cancellationToken: ct));
    public Task<IReadOnlyList<QualificationFlow>> Handle(ListWorkflowsQuery q, CancellationToken ct) => workflowAutomation.ListWorkflowsAsync(q.TenantId, ct);
    public async Task<SalesPipelinesDto> Handle(GetSalesPipelinesQuery q, CancellationToken ct) => new(await db.Pipelines.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct), await db.PipelineStages.AsNoTracking().Where(x => x.TenantId == q.TenantId).OrderBy(x => x.SortOrder).ToListAsync(ct));
    public async Task<IReadOnlyList<MeetingBooking>> Handle(ListMeetingsQuery q, CancellationToken ct) => await db.MeetingBookings.AsNoTracking().Where(x => x.TenantId == q.TenantId).OrderBy(x => x.StartsAtUtc).ToListAsync(ct);
    public async Task<IReadOnlyList<IntegrationConnection>> Handle(ListIntegrationsQuery q, CancellationToken ct) => await db.IntegrationConnections.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public Task<IReadOnlyList<AutomationRule>> Handle(ListAutomationsQuery q, CancellationToken ct) => workflowAutomation.ListAutomationRulesAsync(q.TenantId, ct);
    public async Task<IReadOnlyList<EvaluationDataset>> Handle(ListEvaluationDatasetsQuery q, CancellationToken ct) => await db.EvaluationDatasets.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public async Task<AnalyticsOverviewDto> Handle(GetAnalyticsOverviewQuery q, CancellationToken ct)
    {
        var leads = await db.Leads.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
        var opportunities = await db.Opportunitys.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
        return new(leads.Count(x => x.Score >= 50), leads.Count(x => x.Score >= 80), opportunities.Where(x => x.Status == OpportunityStatus.Open).Sum(x => x.Amount), opportunities.Where(x => x.Status == OpportunityStatus.Won).Sum(x => x.Amount), await db.Conversations.CountAsync(x => x.TenantId == q.TenantId && x.AiEnabled, ct), await db.Tickets.CountAsync(x => x.TenantId == q.TenantId, ct));
    }
    public async Task<IReadOnlyList<Plan>> Handle(ListBillingPlansQuery q, CancellationToken ct) => await db.Plans.AsNoTracking().OrderBy(x => x.MonthlyPrice).ToListAsync(ct);
    public async Task<IReadOnlyList<UsageMeterDto>> Handle(GetBillingUsageQuery q, CancellationToken ct) => await db.UsageRecords.AsNoTracking().Where(x => x.TenantId == q.TenantId).GroupBy(x => x.Meter).Select(g => new UsageMeterDto(g.Key, g.Sum(x => x.Quantity))).ToListAsync(ct);
    public async Task<IReadOnlyList<SsoConfiguration>> Handle(ListSsoConfigurationsQuery q, CancellationToken ct) => await db.SsoConfigurations.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public async Task<IReadOnlyList<DataRetentionPolicy>> Handle(ListRetentionPoliciesQuery q, CancellationToken ct) => await db.DataRetentionPolicys.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public Task<BrandingProfile?> Handle(GetBrandingQuery q, CancellationToken ct) => db.BrandingProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == q.TenantId, ct);
    public async Task<IReadOnlyList<CustomDomain>> Handle(ListCustomDomainsQuery q, CancellationToken ct) => await db.CustomDomains.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public async Task<IReadOnlyList<IndustryPack>> Handle(ListIndustryPacksQuery q, CancellationToken ct) => await db.IndustryPacks.AsNoTracking().ToListAsync(ct);
    public async Task<IReadOnlyList<AuditLog>> Handle(ListAuditLogsQuery q, CancellationToken ct) => await db.AuditLogs.AsNoTracking().Where(x => x.TenantId == q.TenantId).OrderByDescending(x => x.CreatedAtUtc).Take(q.Take).ToListAsync(ct);
    public async Task<IReadOnlyList<CrmTask>> Handle(ListSalesTasksQuery q, CancellationToken ct) => await db.CrmTasks.AsNoTracking().Where(x => x.TenantId == q.TenantId).OrderBy(x => x.Completed).ThenBy(x => x.DueAtUtc).Take(q.Take).ToListAsync(ct);
    public async Task<IReadOnlyList<RevenueAttribution>> Handle(ListRevenueAttributionQuery q, CancellationToken ct) => await db.RevenueAttributions.AsNoTracking().Where(x => x.TenantId == q.TenantId).OrderByDescending(x => x.CreatedAtUtc).Take(q.Take).ToListAsync(ct);
}

public sealed class BusinessModuleCommandHandlers(AppDbContext db) :
    IRequestHandler<CreateIntegrationCommand, IntegrationConnection>,
    IRequestHandler<UpdateIntegrationCommand, IntegrationConnection?>,
    IRequestHandler<TestIntegrationCommand, IntegrationTestResult?>,
    IRequestHandler<UpdateBrandingCommand, BrandingProfile>,
    IRequestHandler<InstallIndustryPackCommand, bool>,
    IRequestHandler<CreateMeetingCommand, MeetingBooking>,
    IRequestHandler<UpdateSalesTaskCommand, CrmTask?>
{
    public async Task<IntegrationConnection> Handle(CreateIntegrationCommand c, CancellationToken ct) { c.Connection.Id = Guid.NewGuid(); c.Connection.TenantId = c.TenantId; db.IntegrationConnections.Add(c.Connection); await db.SaveChangesAsync(ct); return c.Connection; }
    public async Task<IntegrationConnection?> Handle(UpdateIntegrationCommand c, CancellationToken ct) { var x = await db.IntegrationConnections.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; x.Name = c.Connection.Name; x.Provider = c.Connection.Provider; x.Status = c.Connection.Status; x.SettingsJson = c.Connection.SettingsJson; x.SecretReference = c.Connection.SecretReference; await db.SaveChangesAsync(ct); return x; }
    public async Task<IntegrationTestResult?> Handle(TestIntegrationCommand c, CancellationToken ct) { var x = await db.IntegrationConnections.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; db.IntegrationSyncJobs.Add(new IntegrationSyncJob { TenantId = c.TenantId, ConnectionId = c.Id, Direction = "outbound", EntityType = "connection-test", Status = "completed" }); await db.SaveChangesAsync(ct); return new(true, x.Provider, DateTime.UtcNow); }
    public async Task<BrandingProfile> Handle(UpdateBrandingCommand c, CancellationToken ct) { var x = await db.BrandingProfiles.FirstOrDefaultAsync(v => v.TenantId == c.TenantId, ct); if (x is null) { x = new BrandingProfile { TenantId = c.TenantId }; db.BrandingProfiles.Add(x); } x.ProductName = c.Branding.ProductName; x.LogoUrl = c.Branding.LogoUrl; x.PrimaryColor = c.Branding.PrimaryColor; x.AccentColor = c.Branding.AccentColor; x.SupportEmail = c.Branding.SupportEmail; await db.SaveChangesAsync(ct); return x; }
    public async Task<bool> Handle(InstallIndustryPackCommand c, CancellationToken ct) { if (!await db.IndustryPacks.AnyAsync(x => x.Id == c.Id, ct)) return false; if (!await db.TenantIndustryPacks.AnyAsync(x => x.TenantId == c.TenantId && x.IndustryPackId == c.Id, ct)) { db.TenantIndustryPacks.Add(new TenantIndustryPack { TenantId = c.TenantId, IndustryPackId = c.Id, Enabled = true }); await db.SaveChangesAsync(ct); } return true; }
    public async Task<MeetingBooking> Handle(CreateMeetingCommand c, CancellationToken ct) { c.Meeting.Id = Guid.NewGuid(); c.Meeting.TenantId = c.TenantId; if (c.Meeting.EndsAtUtc <= c.Meeting.StartsAtUtc) c.Meeting.EndsAtUtc = c.Meeting.StartsAtUtc.AddMinutes(30); db.MeetingBookings.Add(c.Meeting); await db.SaveChangesAsync(ct); return c.Meeting; }
    public async Task<CrmTask?> Handle(UpdateSalesTaskCommand c, CancellationToken ct) { var x = await db.CrmTasks.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; x.Title = c.Task.Title; x.DueAtUtc = c.Task.DueAtUtc; x.OwnerUserId = c.Task.OwnerUserId; x.Completed = c.Task.Completed; await db.SaveChangesAsync(ct); return x; }
}
