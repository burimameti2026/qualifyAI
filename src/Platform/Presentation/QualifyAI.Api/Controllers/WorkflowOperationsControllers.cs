using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Modules;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.Automation;
using QualifyAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using QualifyAI.Automation.Application.IntegrationEvents;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Automation)]
[Route("api/workflows")]
public sealed class WorkflowsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<IReadOnlyList<QualificationFlow>> List(CancellationToken ct) => sender.Send(new ListWorkflowsQuery(tenant.TenantId()), ct);

    [HttpGet("{id:guid}/designer")]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<WorkflowDesignerDto> Designer(Guid id, CancellationToken ct) => sender.Send(new GetWorkflowDesignerQuery(tenant.TenantId(), id), ct);

    [HttpPut("{id:guid}/designer")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public Task<WorkflowSaveResult> Save(Guid id, WorkflowDesignerInput input, CancellationToken ct)
        => sender.Send(new SaveWorkflowDesignerCommand(tenant.TenantId(), id, input.Nodes, input.Edges), ct);
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Automation)]
[Route("api/automations")]
public sealed class AutomationsController(ISender sender, ITenantContext tenant, AppDbContext db, AutomationActionExecutor executor, IPublishEndpoint publisher) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<IReadOnlyList<AutomationRule>> List(CancellationToken ct) => sender.Send(new ListAutomationsQuery(tenant.TenantId()), ct);

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public Task<AutomationRule> Create(AutomationRule input, CancellationToken ct) => sender.Send(new CreateAutomationCommand(tenant.TenantId(), input), ct);

    [HttpPut("{id:guid}")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Update(Guid id, AutomationRule input, CancellationToken ct)
        => (await sender.Send(new UpdateAutomationCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("{id:guid}/run")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Run(Guid id, CancellationToken ct)
        => (await sender.Send(new RunAutomationCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("{id:guid}/publish-trigger")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> PublishTrigger(Guid id, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        if (!await db.AutomationRules.AnyAsync(x => x.TenantId == tenantId && x.Id == id && x.Active, ct)) return NotFound();
        var eventId = Guid.NewGuid();
        await publisher.Publish(new AutomationTriggeredIntegrationEvent(eventId, tenantId, DateTime.UtcNow, Guid.NewGuid(), id), ct);
        return Accepted(new { eventId, ruleId = id, transport = "rabbitmq" });
    }

    [HttpGet("runs")]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<List<AutomationRun>> Runs(CancellationToken ct) => db.AutomationRuns.AsNoTracking()
        .Where(x => x.TenantId == tenant.TenantId()).OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);

    [HttpGet("dead-letters")]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<List<IntegrationSyncJob>> DeadLetters(CancellationToken ct) => db.IntegrationSyncJobs.AsNoTracking()
        .Where(x => x.TenantId == tenant.TenantId() && x.Status == "dead-letter")
        .OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);

    [HttpPost("runs/{runId:guid}/retry")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Retry(Guid runId, CancellationToken ct)
    {
        var oldRun = await db.AutomationRuns.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId() && x.Id == runId, ct);
        if (oldRun is null) return NotFound();
        if (!string.Equals(oldRun.Status, "failed", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { code = "run_not_failed", detail = "Only failed automation runs can be retried." });
        var rule = await db.AutomationRules.FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId() && x.Id == oldRun.RuleId, ct);
        if (rule is null) return NotFound();
        var retry = AutomationRun.Create(rule.TenantId, rule.Id, oldRun.TriggerDataJson);
        db.AutomationRuns.Add(retry); retry.Start(); await db.SaveChangesAsync(ct);
        var result = await executor.ExecuteAsync(rule, retry, ct);
        if (result.Success) retry.Complete(result.LogJson); else retry.Fail(result.LogJson);
        await db.SaveChangesAsync(ct);
        return Ok(retry);
    }
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Integrations)]
[Route("api/integrations")]
public sealed class IntegrationsController(ISender sender, ITenantContext tenant, IIntegrationRegistry registry) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.IntegrationsRead)]
    public Task<IReadOnlyList<IntegrationConnection>> List(CancellationToken ct) => sender.Send(new ListIntegrationsQuery(tenant.TenantId()), ct);

    [HttpGet("providers")]
    [RequirePermission(QualifyAiPermissions.IntegrationsRead)]
    public IActionResult Providers() => Ok(registry.Providers);

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public Task<IntegrationConnection> Create(IntegrationConnection input, CancellationToken ct) => sender.Send(new CreateIntegrationCommand(tenant.TenantId(), input), ct);

    [HttpPut("{id:guid}")]
    [RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> Update(Guid id, IntegrationConnection input, CancellationToken ct)
        => (await sender.Send(new UpdateIntegrationCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("{id:guid}/test")]
    [RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> Test(Guid id, CancellationToken ct)
        => (await sender.Send(new TestIntegrationCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/meetings")]
public sealed class MeetingsController(ISender sender, ITenantContext tenant, AppDbContext db) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<MeetingBooking>> List(CancellationToken ct) => sender.Send(new ListMeetingsQuery(tenant.TenantId()), ct);

    [HttpGet("types")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<List<MeetingType>> Types(CancellationToken ct) => db.MeetingTypes.AsNoTracking().Where(x => x.TenantId == tenant.TenantId()).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Create(MeetingBooking input, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        if (input.ContactId.HasValue && !await db.Contacts.AnyAsync(x => x.TenantId == tenantId && x.Id == input.ContactId, ct))
            return BadRequest(new { detail = "The selected contact does not belong to this tenant." });
        if (input.LeadId.HasValue && !await db.Leads.AnyAsync(x => x.TenantId == tenantId && x.Id == input.LeadId, ct))
            return BadRequest(new { detail = "The selected lead does not belong to this tenant." });
        if (input.MeetingTypeId == Guid.Empty)
        {
            var defaultType = await db.MeetingTypes.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == "Discovery call", ct);
            if (defaultType is null)
            {
                defaultType = new MeetingType { TenantId = tenantId, Name = "Discovery call", DurationMinutes = 30, LocationType = "video" };
                db.MeetingTypes.Add(defaultType);
            }
            input.MeetingTypeId = defaultType.Id;
        }
        else if (!await db.MeetingTypes.AnyAsync(x => x.TenantId == tenantId && x.Id == input.MeetingTypeId, ct))
            return BadRequest(new { detail = "The selected meeting type does not belong to this tenant." });
        var x = await sender.Send(new CreateMeetingCommand(tenant.TenantId(), input), ct);
        db.AuditLogs.Add(new AuditLog { TenantId = tenantId, Action = "sales.meeting.booked", EntityType = nameof(MeetingBooking), EntityId = x.Id.ToString(), DataJson = "{}" });
        await db.SaveChangesAsync(ct);
        return Created($"/api/meetings/{x.Id}", x);
    }
}

public sealed record WorkflowDesignerInput(List<WorkflowNode> Nodes, List<WorkflowEdge> Edges);
