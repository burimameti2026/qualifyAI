using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Commands.Modules;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Infrastructure.Automation;
using LeadsAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using LeadsAI.Automation.Application.IntegrationEvents;

namespace LeadsAI.Api.Controllers;

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
