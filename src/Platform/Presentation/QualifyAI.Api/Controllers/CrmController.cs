using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Commands;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries;
using QualifyAI.Application.Queries.Crm;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace QualifyAI.Api.Controllers;

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

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/crm")]
public sealed class CrmController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("contacts")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<Contact>> Contacts(CancellationToken ct) => sender.Send(new ListContactsQuery(tenant.TenantId()), ct);

    [HttpPost("contacts")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateContact(Contact input, CancellationToken ct)
    {
        try
        {
            var x = await sender.Send(new CreateContactCommand(tenant.TenantId(), input.CompanyId, input.FirstName, input.LastName, input.Email, input.Phone, input.LifecycleStage), ct);
            return Created($"/api/crm/contacts/{x.Id}", x);
        }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("contacts/{id:guid}")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdateContact(Guid id, Contact input, CancellationToken ct)
    {
        try { return (await sender.Send(new UpdateContactCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("companies")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<Company>> Companies(CancellationToken ct) => sender.Send(new ListCompaniesQuery(tenant.TenantId()), ct);

    [HttpPost("companies")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateCompany(Company input, CancellationToken ct)
    {
        try { var x = await sender.Send(new CreateCompanyCommand(tenant.TenantId(), input), ct); return Created($"/api/crm/companies/{x.Id}", x); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpGet("leads")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<Lead>> Leads(CancellationToken ct) => sender.Send(new ListLeadsQuery(tenant.TenantId()), ct);

    [HttpGet("opportunities")][RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<Opportunity>> Opportunities(CancellationToken ct) => sender.Send(new ListOpportunitiesQuery(tenant.TenantId()), ct);

    [HttpPut("opportunities/{id:guid}")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdateOpportunity(Guid id, Opportunity input, CancellationToken ct)
    {
        try { return (await sender.Send(new UpdateOpportunityCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPut("opportunities/{id:guid}/stage")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> MoveOpportunity(Guid id, OpportunityStageInput input, CancellationToken ct)
    {
        try { return (await sender.Send(new MoveOpportunityStageCommand(tenant.TenantId(), id, input.StageId), ct)) is { } x ? Ok(x) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("opportunities/{id:guid}/close")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CloseOpportunity(Guid id, OpportunityCloseInput input, CancellationToken ct)
    {
        try { return (await sender.Send(new CloseOpportunityCommand(tenant.TenantId(), id, input.Won, input.LossReason), ct)) is { } x ? Ok(x) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }

    [HttpPost("opportunities/{id:guid}/reopen")][RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> ReopenOpportunity(Guid id, CancellationToken ct)
        => (await sender.Send(new ReopenOpportunityCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();
}

public sealed record OpportunityStageInput(Guid StageId);
public sealed record OpportunityCloseInput(bool Won, string? LossReason);
