using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Infrastructure.Email;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api.Controllers;

[ApiController, Authorize, RequireModule(QualifyAiModules.Integrations)]
[Route("api/email-operations")]
public sealed class EmailOperationsController(
    AppDbContext db,
    ITenantContext tenant,
    EmailDeliveryService delivery) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpPost("messages/{id:guid}/request-approval"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> RequestApproval(Guid id, CancellationToken ct)
    {
        var message = await LoadQualifiedMessageAsync(id, ct);
        if (message is null) return NotFound();
        if (message.Status != OutreachStatus.Queued)
            return Conflict(new { detail = "Only queued qualified outreach messages can enter approval." });

        var title = $"APPROVAL: Send outreach {id}";
        var task = await db.CrmTasks.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Title == title, ct);
        if (task is null)
        {
            task = new CrmTask { TenantId = TenantId, Title = title, DueAtUtc = DateTime.UtcNow.AddHours(4) };
            db.CrmTasks.Add(task);
        }
        else if (task.Completed)
        {
            task.Completed = false;
            task.DueAtUtc = DateTime.UtcNow.AddHours(4);
        }

        await db.SaveChangesAsync(ct);
        return Ok(task);
    }

    [HttpPost("messages/{id:guid}/approve-and-send"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> ApproveAndSend(Guid id, CancellationToken ct)
    {
        var message = await LoadQualifiedMessageAsync(id, ct);
        if (message is null) return NotFound();

        var task = await db.CrmTasks.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Title == $"APPROVAL: Send outreach {id}" && !x.Completed, ct);
        if (task is null) return BadRequest(new { detail = "Request approval before sending this message." });

        task.Completed = true;
        await db.SaveChangesAsync(ct);

        var result = await delivery.SendApprovedAsync(TenantId, id, ct);
        return result.Success ? Ok(result) : Conflict(new { detail = result.Error });
    }

    [HttpPost("messages/{id:guid}/reject"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> Reject(Guid id, CancellationToken ct)
    {
        var message = await LoadQualifiedMessageAsync(id, ct);
        if (message is null) return NotFound();
        if (message.Status != OutreachStatus.Queued)
            return Conflict(new { detail = "Only queued outreach messages can be rejected." });

        message.Status = OutreachStatus.Suppressed;
        var task = await db.CrmTasks.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Title == $"APPROVAL: Send outreach {id}", ct);
        if (task is not null) task.Completed = true;
        await db.SaveChangesAsync(ct);
        return Ok(new { message.Id, status = message.Status.ToString(), rejected = true });
    }

    [HttpPost("messages/{id:guid}/retry"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> Retry(Guid id, CancellationToken ct)
    {
        var message = await LoadQualifiedMessageAsync(id, ct);
        if (message is null) return NotFound();
        if (message.Status != OutreachStatus.Failed)
            return Conflict(new { detail = "Only failed qualified outreach messages can be retried." });

        message.Status = OutreachStatus.Queued;
        var title = $"APPROVAL: Send outreach {id}";
        var task = await db.CrmTasks.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Title == title, ct);
        if (task is null)
            db.CrmTasks.Add(new CrmTask { TenantId = TenantId, Title = title, DueAtUtc = DateTime.UtcNow.AddHours(4) });
        else
        {
            task.Completed = false;
            task.DueAtUtc = DateTime.UtcNow.AddHours(4);
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { message.Id, status = message.Status.ToString(), approvalRequired = true });
    }

    private async Task<OutreachMessage?> LoadQualifiedMessageAsync(Guid id, CancellationToken ct)
    {
        var message = await db.OutreachMessages.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == id, ct);
        if (message is null) return null;

        var qualified = await db.Prospects.AnyAsync(
            x => x.TenantId == TenantId && x.Id == message.ProspectId && x.Status == ProspectStatus.Qualified, ct);
        return qualified ? message : null;
    }
}
