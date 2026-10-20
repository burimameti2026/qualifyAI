using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Queries;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using Microsoft.EntityFrameworkCore;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Analytics)]
[RequirePermission(QualifyAiPermissions.AnalyticsRead)]
[Route("api")]
public sealed class DashboardController(ISender sender, ITenantContext tenant, AppDbContext db) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var x = await sender.Send(new DashboardOverviewQuery(tenant.TenantId()), ct);
        var tenantId = tenant.TenantId();
        var opportunities = await (
            from opportunity in db.Opportunitys
            join company in db.Companys on opportunity.CompanyId equals (Guid?)company.Id into companies
            from company in companies.DefaultIfEmpty()
            join lead in db.Leads on opportunity.LeadId equals (Guid?)lead.Id into leads
            from lead in leads.DefaultIfEmpty()
            where opportunity.TenantId == tenantId && opportunity.Status == OpportunityStatus.Open
            orderby lead.Score descending
            select new { id = opportunity.Id, company = company == null ? opportunity.Name : company.Name, country = company == null ? "" : company.Country, intent = lead == null ? "Qualified opportunity" : lead.IntentSummary, score = lead == null ? 0 : lead.Score, value = opportunity.Amount }
        ).Take(5).ToListAsync(ct);
        var gaps = await db.KnowledgeGaps.AsNoTracking().Where(g => g.TenantId == tenantId && g.Status != "resolved")
            .OrderByDescending(g => g.ImpactScore).Take(5).Select(g => new { g.Topic, count = g.Occurrences, impact = g.ImpactScore >= 80 ? "High" : "Medium" }).ToListAsync(ct);
        var actions = await db.UsageRecords.Where(u => u.TenantId == tenantId && u.Meter == "automation_actions").SumAsync(u => (decimal?)u.Quantity, ct) ?? 0;
        var meetings = await db.MeetingBookings.CountAsync(m => m.TenantId == tenantId && m.Status == "booked", ct);
        var influenced = await db.RevenueAttributions.Where(r => r.TenantId == tenantId).SumAsync(r => (decimal?)r.InfluencedRevenue, ct) ?? x.Pipeline;
        var won = await db.Opportunitys.Where(o => o.TenantId == tenantId && o.Status == OpportunityStatus.Won).SumAsync(o => (decimal?)o.Amount, ct) ?? 0;
        var completedRuns = await db.AutomationRuns.CountAsync(r => r.TenantId == tenantId && r.Status == "completed", ct);
        return Ok(new
        {
            contacts=x.Contacts, leads=x.Leads, hotLeads=x.HotLeads, openConversations=x.OpenConversations,
            openTickets=x.OpenTickets, pipeline=x.Pipeline, influencedRevenue=influenced, wonRevenue=won,
            automationActions=actions, meetingsBooked=meetings, completedRuns,
            estimatedHoursSaved=Math.Round(actions * 0.15m, 1), opportunities, knowledgeGaps=gaps
        });
    }
}
