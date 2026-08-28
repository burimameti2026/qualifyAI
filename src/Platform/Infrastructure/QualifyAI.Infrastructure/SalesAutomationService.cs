using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;

namespace QualifyAI.Infrastructure;

public sealed record SalesAutomationResult(int Processed, int OpportunitiesCreated, int TasksCreated, int AuditEvents, decimal PipelineCreated);

public sealed class SalesAutomationService(AppDbContext db)
{
    public async Task<SalesAutomationResult> RunAsync(Guid tenantId, CancellationToken ct = default)
    {
        var leads = await db.Leads.Where(x => x.TenantId == tenantId && x.Status != "won" && x.Status != "lost").ToListAsync(ct);
        var stages = await db.PipelineStages.Where(x => x.TenantId == tenantId).OrderBy(x => x.SortOrder).ToListAsync(ct);
        var qualifiedStage = stages.FirstOrDefault(x => x.Name.Contains("Qualified")) ?? stages.Skip(1).FirstOrDefault() ?? stages.FirstOrDefault();
        var createdOpps = 0; var createdTasks = 0; var audits = 0; decimal pipelineCreated = 0;

        foreach (var lead in leads)
        {
            var previousStatus = lead.Status;
            lead.Temperature = lead.Score >= 80 ? LeadTemperature.Hot : lead.Score >= 50 ? LeadTemperature.Warm : LeadTemperature.Cold;
            lead.Status = lead.Score >= 80 ? "qualified" : lead.Score >= 50 ? "nurture" : lead.Status;

            if (lead.Score < 80) continue;

            var opportunity = await db.Opportunitys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id && x.Status == OpportunityStatus.Open, ct);
            if (opportunity is null)
            {
                opportunity = new Opportunity
                {
                    TenantId = tenantId,
                    LeadId = lead.Id,
                    CompanyId = lead.CompanyId,
                    ContactId = lead.ContactId,
                    PipelineStageId = qualifiedStage?.Id,
                    Name = string.IsNullOrWhiteSpace(lead.IntentSummary) ? "AI-qualified opportunity" : lead.IntentSummary,
                    Amount = lead.EstimatedValue ?? Math.Max(5000, lead.Score * 250),
                    Status = OpportunityStatus.Open,
                    ExpectedCloseUtc = DateTime.UtcNow.AddDays(21)
                };
                db.Opportunitys.Add(opportunity);
                db.RevenueAttributions.Add(new RevenueAttribution
                {
                    TenantId = tenantId,
                    LeadId = lead.Id,
                    OpportunityId = opportunity.Id,
                    InfluencedRevenue = opportunity.Amount,
                    Model = "ai-qualified"
                });
                createdOpps++;
                pipelineCreated += opportunity.Amount;
            }

            var taskExists = await db.CrmTasks.AnyAsync(x => x.TenantId == tenantId && x.LeadId == lead.Id && !x.Completed, ct);
            if (!taskExists)
            {
                db.CrmTasks.Add(new CrmTask
                {
                    TenantId = tenantId,
                    LeadId = lead.Id,
                    ContactId = lead.ContactId,
                    Title = $"Follow up hot lead ({lead.Score}/100)",
                    DueAtUtc = DateTime.UtcNow.AddHours(2)
                });
                createdTasks++;
            }

            if (opportunity.Id == Guid.Empty || !string.Equals(previousStatus, lead.Status, StringComparison.OrdinalIgnoreCase) || !taskExists)
            {
                db.AuditLogs.Add(new AuditLog
                {
                    TenantId = tenantId,
                    Action = "sales.automation.hot_lead",
                    EntityType = nameof(Lead),
                    EntityId = lead.Id.ToString(),
                    DataJson = $"{{\"score\":{lead.Score},\"status\":\"{lead.Status}\",\"opportunity\":\"{opportunity.Id}\"}}"
                });
                audits++;
            }
        }

        await db.SaveChangesAsync(ct);
        return new SalesAutomationResult(leads.Count, createdOpps, createdTasks, audits, pipelineCreated);
    }

    public async Task<Opportunity?> ConvertLeadAsync(Guid tenantId, Guid leadId, CancellationToken ct = default)
    {
        var lead = await db.Leads.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == leadId, ct);
        if (lead is null) return null;
        var existing = await db.Opportunitys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.LeadId == leadId && x.Status == OpportunityStatus.Open, ct);
        if (existing is not null) return existing;
        var stage = await db.PipelineStages.Where(x => x.TenantId == tenantId).OrderBy(x => x.SortOrder).FirstOrDefaultAsync(ct);
        var opp = new Opportunity
        {
            TenantId = tenantId,
            LeadId = lead.Id,
            CompanyId = lead.CompanyId,
            ContactId = lead.ContactId,
            PipelineStageId = stage?.Id,
            Name = string.IsNullOrWhiteSpace(lead.IntentSummary) ? "Qualified opportunity" : lead.IntentSummary,
            Amount = lead.EstimatedValue ?? Math.Max(5000, lead.Score * 250),
            ExpectedCloseUtc = DateTime.UtcNow.AddDays(21)
        };
        db.Opportunitys.Add(opp);
        db.RevenueAttributions.Add(new RevenueAttribution { TenantId = tenantId, LeadId = lead.Id, OpportunityId = opp.Id, InfluencedRevenue = opp.Amount, Model = "manual-conversion" });
        lead.Status = "qualified";
        db.CrmTasks.Add(new CrmTask { TenantId = tenantId, LeadId = lead.Id, ContactId = lead.ContactId, Title = "Contact qualified lead", DueAtUtc = DateTime.UtcNow.AddHours(2) });
        db.AuditLogs.Add(new AuditLog { TenantId = tenantId, Action = "sales.lead.converted", EntityType = nameof(Lead), EntityId = lead.Id.ToString(), DataJson = $"{{\"amount\":{opp.Amount}}}" });
        await db.SaveChangesAsync(ct);
        return opp;
    }
}
