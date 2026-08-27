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

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Analytics)]
[RequirePermission(QualifyAiPermissions.AnalyticsRead)]
[Route("api")]
public sealed class DashboardController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var x = await sender.Send(new DashboardOverviewQuery(tenant.TenantId()), ct);
        return Ok(new { contacts=x.Contacts, leads=x.Leads, hotLeads=x.HotLeads, openConversations=x.OpenConversations, openTickets=x.OpenTickets, pipeline=x.Pipeline });
    }
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/crm")]
public sealed class CrmController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("contacts")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<Contact>> Contacts(CancellationToken ct) => sender.Send(new ListContactsQuery(tenant.TenantId()), ct);

    [HttpPost("contacts")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateContact(Contact input, CancellationToken ct)
    {
        var x = await sender.Send(new CreateContactCommand(tenant.TenantId(), input.CompanyId, input.FirstName, input.LastName, input.Email, input.Phone, input.LifecycleStage), ct);
        return Created($"/api/crm/contacts/{x.Id}", x);
    }

    [HttpPut("contacts/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdateContact(Guid id, Contact input, CancellationToken ct)
        => (await sender.Send(new UpdateContactCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpGet("companies")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<Company>> Companies(CancellationToken ct) => sender.Send(new ListCompaniesQuery(tenant.TenantId()), ct);

    [HttpPost("companies")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateCompany(Company input, CancellationToken ct)
    {
        var x = await sender.Send(new CreateCompanyCommand(tenant.TenantId(), input), ct);
        return Created($"/api/crm/companies/{x.Id}", x);
    }

    [HttpGet("leads")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<Lead>> Leads(CancellationToken ct) => sender.Send(new ListLeadsQuery(tenant.TenantId()), ct);

    [HttpGet("opportunities")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<Opportunity>> Opportunities(CancellationToken ct) => sender.Send(new ListOpportunitiesQuery(tenant.TenantId()), ct);

    [HttpPut("opportunities/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdateOpportunity(Guid id, Opportunity input, CancellationToken ct)
        => (await sender.Send(new UpdateOpportunityCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPut("opportunities/{id:guid}/stage")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> MoveOpportunity(Guid id, OpportunityStageInput input, CancellationToken ct)
    {
        try { return (await sender.Send(new MoveOpportunityStageCommand(tenant.TenantId(), id, input.StageId), ct)) is { } x ? Ok(x) : NotFound(); }
        catch (InvalidOperationException ex) { return BadRequest(new { error = ex.Message }); }
    }
}

public sealed record OpportunityStageInput(Guid StageId);
