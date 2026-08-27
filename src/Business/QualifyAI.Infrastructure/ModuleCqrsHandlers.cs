using MediatR;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Modules;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Persistence;

namespace QualifyAI.Infrastructure;

public sealed class BusinessModuleQueryHandlers(AppDbContext db) :
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
    public async Task<IReadOnlyList<KnowledgeBase>> Handle(ListKnowledgeBasesQuery q, CancellationToken ct) => await db.KnowledgeBases.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public async Task<IReadOnlyList<KnowledgeDocument>> Handle(ListKnowledgeDocumentsQuery q, CancellationToken ct) => await db.KnowledgeDocuments.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public async Task<IReadOnlyList<KnowledgeGap>> Handle(ListKnowledgeGapsQuery q, CancellationToken ct) => await db.KnowledgeGaps.AsNoTracking().Where(x => x.TenantId == q.TenantId).OrderByDescending(x => x.ImpactScore).ToListAsync(ct);
    public async Task<IReadOnlyList<AiAgent>> Handle(ListAiAgentsQuery q, CancellationToken ct) => await db.AiAgents.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public async Task<WorkflowDesignerDto> Handle(GetWorkflowDesignerQuery q, CancellationToken ct) => new(await db.WorkflowNodes.AsNoTracking().Where(x => x.TenantId == q.TenantId && x.FlowId == q.FlowId).ToListAsync(ct), await db.WorkflowEdges.AsNoTracking().Where(x => x.TenantId == q.TenantId && x.FlowId == q.FlowId).ToListAsync(ct));
    public async Task<IReadOnlyList<QualificationFlow>> Handle(ListWorkflowsQuery q, CancellationToken ct) => await db.QualificationFlows.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public async Task<SalesPipelinesDto> Handle(GetSalesPipelinesQuery q, CancellationToken ct) => new(await db.Pipelines.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct), await db.PipelineStages.AsNoTracking().Where(x => x.TenantId == q.TenantId).OrderBy(x => x.SortOrder).ToListAsync(ct));
    public async Task<IReadOnlyList<MeetingBooking>> Handle(ListMeetingsQuery q, CancellationToken ct) => await db.MeetingBookings.AsNoTracking().Where(x => x.TenantId == q.TenantId).OrderBy(x => x.StartsAtUtc).ToListAsync(ct);
    public async Task<IReadOnlyList<IntegrationConnection>> Handle(ListIntegrationsQuery q, CancellationToken ct) => await db.IntegrationConnections.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
    public async Task<IReadOnlyList<AutomationRule>> Handle(ListAutomationsQuery q, CancellationToken ct) => await db.AutomationRules.AsNoTracking().Where(x => x.TenantId == q.TenantId).ToListAsync(ct);
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
    IRequestHandler<CreateKnowledgeDocumentCommand, KnowledgeDocument>,
    IRequestHandler<UpdateKnowledgeDocumentCommand, KnowledgeDocument?>,
    IRequestHandler<ReindexKnowledgeDocumentCommand, ReindexResult?>,
    IRequestHandler<UpdateKnowledgeGapCommand, KnowledgeGap?>,
    IRequestHandler<CreateAiAgentCommand, AiAgent>,
    IRequestHandler<UpdateAiAgentCommand, AiAgent?>,
    IRequestHandler<SaveWorkflowDesignerCommand, WorkflowSaveResult>,
    IRequestHandler<CreateAutomationCommand, AutomationRule>,
    IRequestHandler<UpdateAutomationCommand, AutomationRule?>,
    IRequestHandler<RunAutomationCommand, AutomationRun?>,
    IRequestHandler<CreateIntegrationCommand, IntegrationConnection>,
    IRequestHandler<UpdateIntegrationCommand, IntegrationConnection?>,
    IRequestHandler<TestIntegrationCommand, IntegrationTestResult?>,
    IRequestHandler<UpdateBrandingCommand, BrandingProfile>,
    IRequestHandler<InstallIndustryPackCommand, bool>,
    IRequestHandler<CreateMeetingCommand, MeetingBooking>,
    IRequestHandler<UpdateSalesTaskCommand, CrmTask?>
{
    public async Task<KnowledgeDocument> Handle(CreateKnowledgeDocumentCommand c, CancellationToken ct) { c.Document.Id = Guid.NewGuid(); c.Document.TenantId = c.TenantId; db.KnowledgeDocuments.Add(c.Document); await db.SaveChangesAsync(ct); return c.Document; }
    public async Task<KnowledgeDocument?> Handle(UpdateKnowledgeDocumentCommand c, CancellationToken ct) { var x = await db.KnowledgeDocuments.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; x.Title = c.Document.Title; x.Body = c.Document.Body; x.Published = c.Document.Published; x.Version = Math.Max(x.Version + 1, c.Document.Version); await db.SaveChangesAsync(ct); return x; }
    public async Task<ReindexResult?> Handle(ReindexKnowledgeDocumentCommand c, CancellationToken ct) { var x = await db.KnowledgeDocuments.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; var old = await db.KnowledgeChunks.Where(v => v.DocumentId == c.Id && v.TenantId == c.TenantId).ToListAsync(ct); db.KnowledgeChunks.RemoveRange(old); var chunks = (x.Body ?? string.Empty).Split(new[] { "\n\n", ". " }, StringSplitOptions.RemoveEmptyEntries).Take(100).ToArray(); for (var i = 0; i < chunks.Length; i++) db.KnowledgeChunks.Add(new KnowledgeChunk { TenantId = c.TenantId, DocumentId = c.Id, ChunkIndex = i, Text = chunks[i], VectorJson = "[]" }); await db.SaveChangesAsync(ct); return new(c.Id, chunks.Length, "indexed"); }
    public async Task<KnowledgeGap?> Handle(UpdateKnowledgeGapCommand c, CancellationToken ct) { var x = await db.KnowledgeGaps.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; x.Status = c.Gap.Status; await db.SaveChangesAsync(ct); return x; }
    public async Task<AiAgent> Handle(CreateAiAgentCommand c, CancellationToken ct) { c.Agent.Id = Guid.NewGuid(); c.Agent.TenantId = c.TenantId; db.AiAgents.Add(c.Agent); await db.SaveChangesAsync(ct); return c.Agent; }
    public async Task<AiAgent?> Handle(UpdateAiAgentCommand c, CancellationToken ct) { var x = await db.AiAgents.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; x.Name = c.Agent.Name; x.Role = c.Agent.Role; x.Instructions = c.Agent.Instructions; x.Tone = c.Agent.Tone; x.Model = c.Agent.Model; x.LanguagesCsv = c.Agent.LanguagesCsv; x.Active = c.Agent.Active; x.KnowledgeBaseId = c.Agent.KnowledgeBaseId; await db.SaveChangesAsync(ct); return x; }
    public async Task<WorkflowSaveResult> Handle(SaveWorkflowDesignerCommand c, CancellationToken ct) { var oldN = await db.WorkflowNodes.Where(x => x.TenantId == c.TenantId && x.FlowId == c.FlowId).ToListAsync(ct); var oldE = await db.WorkflowEdges.Where(x => x.TenantId == c.TenantId && x.FlowId == c.FlowId).ToListAsync(ct); db.WorkflowNodes.RemoveRange(oldN); db.WorkflowEdges.RemoveRange(oldE); foreach (var n in c.Nodes) { n.Id = n.Id == Guid.Empty ? Guid.NewGuid() : n.Id; n.TenantId = c.TenantId; n.FlowId = c.FlowId; db.WorkflowNodes.Add(n); } foreach (var e in c.Edges) { e.Id = e.Id == Guid.Empty ? Guid.NewGuid() : e.Id; e.TenantId = c.TenantId; e.FlowId = c.FlowId; db.WorkflowEdges.Add(e); } await db.SaveChangesAsync(ct); return new(c.Nodes.Count, c.Edges.Count); }
    public async Task<AutomationRule> Handle(CreateAutomationCommand c, CancellationToken ct) { c.Rule.Id = Guid.NewGuid(); c.Rule.TenantId = c.TenantId; db.AutomationRules.Add(c.Rule); await db.SaveChangesAsync(ct); return c.Rule; }
    public async Task<AutomationRule?> Handle(UpdateAutomationCommand c, CancellationToken ct) { var x = await db.AutomationRules.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; x.Name = c.Rule.Name; x.Trigger = c.Rule.Trigger; x.ConditionsJson = c.Rule.ConditionsJson; x.ActionsJson = c.Rule.ActionsJson; x.Active = c.Rule.Active; await db.SaveChangesAsync(ct); return x; }
    public async Task<AutomationRun?> Handle(RunAutomationCommand c, CancellationToken ct) { var rule = await db.AutomationRules.FirstOrDefaultAsync(x => x.Id == c.Id && x.TenantId == c.TenantId, ct); if (rule is null) return null; var run = new AutomationRun { TenantId = c.TenantId, RuleId = c.Id, TriggerDataJson = "{\"manual\":true}", Status = "completed", LogJson = "[\"Rule evaluated\",\"Actions dispatched\"]", CompletedAtUtc = DateTime.UtcNow }; db.AutomationRuns.Add(run); await db.SaveChangesAsync(ct); return run; }
    public async Task<IntegrationConnection> Handle(CreateIntegrationCommand c, CancellationToken ct) { c.Connection.Id = Guid.NewGuid(); c.Connection.TenantId = c.TenantId; db.IntegrationConnections.Add(c.Connection); await db.SaveChangesAsync(ct); return c.Connection; }
    public async Task<IntegrationConnection?> Handle(UpdateIntegrationCommand c, CancellationToken ct) { var x = await db.IntegrationConnections.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; x.Name = c.Connection.Name; x.Provider = c.Connection.Provider; x.Status = c.Connection.Status; x.SettingsJson = c.Connection.SettingsJson; x.SecretReference = c.Connection.SecretReference; await db.SaveChangesAsync(ct); return x; }
    public async Task<IntegrationTestResult?> Handle(TestIntegrationCommand c, CancellationToken ct) { var x = await db.IntegrationConnections.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; db.IntegrationSyncJobs.Add(new IntegrationSyncJob { TenantId = c.TenantId, ConnectionId = c.Id, Direction = "outbound", EntityType = "connection-test", Status = "completed" }); await db.SaveChangesAsync(ct); return new(true, x.Provider, DateTime.UtcNow); }
    public async Task<BrandingProfile> Handle(UpdateBrandingCommand c, CancellationToken ct) { var x = await db.BrandingProfiles.FirstOrDefaultAsync(v => v.TenantId == c.TenantId, ct); if (x is null) { x = new BrandingProfile { TenantId = c.TenantId }; db.BrandingProfiles.Add(x); } x.ProductName = c.Branding.ProductName; x.LogoUrl = c.Branding.LogoUrl; x.PrimaryColor = c.Branding.PrimaryColor; x.AccentColor = c.Branding.AccentColor; x.SupportEmail = c.Branding.SupportEmail; await db.SaveChangesAsync(ct); return x; }
    public async Task<bool> Handle(InstallIndustryPackCommand c, CancellationToken ct) { if (!await db.IndustryPacks.AnyAsync(x => x.Id == c.Id, ct)) return false; if (!await db.TenantIndustryPacks.AnyAsync(x => x.TenantId == c.TenantId && x.IndustryPackId == c.Id, ct)) { db.TenantIndustryPacks.Add(new TenantIndustryPack { TenantId = c.TenantId, IndustryPackId = c.Id, Enabled = true }); await db.SaveChangesAsync(ct); } return true; }
    public async Task<MeetingBooking> Handle(CreateMeetingCommand c, CancellationToken ct) { c.Meeting.Id = Guid.NewGuid(); c.Meeting.TenantId = c.TenantId; if (c.Meeting.EndsAtUtc <= c.Meeting.StartsAtUtc) c.Meeting.EndsAtUtc = c.Meeting.StartsAtUtc.AddMinutes(30); db.MeetingBookings.Add(c.Meeting); await db.SaveChangesAsync(ct); return c.Meeting; }
    public async Task<CrmTask?> Handle(UpdateSalesTaskCommand c, CancellationToken ct) { var x = await db.CrmTasks.FirstOrDefaultAsync(v => v.Id == c.Id && v.TenantId == c.TenantId, ct); if (x is null) return null; x.Title = c.Task.Title; x.DueAtUtc = c.Task.DueAtUtc; x.OwnerUserId = c.Task.OwnerUserId; x.Completed = c.Task.Completed; await db.SaveChangesAsync(ct); return x; }
}
