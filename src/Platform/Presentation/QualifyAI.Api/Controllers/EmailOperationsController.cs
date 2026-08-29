using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.Email;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController, Authorize, RequireModule(QualifyAiModules.Integrations)]
[Route("api/email-operations")]
public sealed class EmailOperationsController(AppDbContext db, ITenantContext tenant, EmailDeliveryService delivery) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("senders"), RequirePermission(QualifyAiPermissions.IntegrationsRead)]
    public async Task<IActionResult> Senders(CancellationToken ct) => Ok(await db.IntegrationConnections.AsNoTracking()
        .Where(x => x.TenantId == TenantId && x.Provider == "email-sender").Select(x => new { x.Id, x.Name, x.Status, x.SettingsJson, x.CreatedAtUtc }).ToListAsync(ct));

    [HttpPost("senders"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> ConfigureSender(SenderInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || !input.Email.Contains('@')) return BadRequest(new { detail = "A valid sender email is required." });
        var normalized = input.Email.Trim().ToLowerInvariant();
        var sender = await db.IntegrationConnections.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Provider == "email-sender" && x.Name == normalized, ct);
        var token = Convert.ToHexString(Guid.NewGuid().ToByteArray()).ToLowerInvariant();
        if (sender is null) { sender = new IntegrationConnection { TenantId = TenantId, Provider = "email-sender", Name = normalized }; db.IntegrationConnections.Add(sender); }
        sender.Status = IntegrationStatus.Disconnected;
        sender.SettingsJson = JsonSerializer.Serialize(new { email = normalized, name = input.Name.Trim(), verified = false, verificationToken = token });
        sender.SecretReference = $"Email:{input.Provider}:credentials";
        await db.SaveChangesAsync(ct);
        return Ok(new { sender.Id, sender.Name, sender.Status, verificationToken = token, instruction = "Verify only after proving control of this mailbox/domain." });
    }

    [HttpPost("senders/{id:guid}/verify"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> VerifySender(Guid id, VerifySenderInput input, CancellationToken ct)
    {
        var sender = await db.IntegrationConnections.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.Provider == "email-sender", ct);
        if (sender is null) return NotFound();
        using var settings = JsonDocument.Parse(sender.SettingsJson);
        if (!settings.RootElement.TryGetProperty("verificationToken", out var expected) || expected.GetString() != input.Token) return BadRequest(new { detail = "Verification token is invalid." });
        var email = settings.RootElement.GetProperty("email").GetString() ?? "";
        var name = settings.RootElement.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        sender.SettingsJson = JsonSerializer.Serialize(new { email, name, verified = true, verifiedAtUtc = DateTime.UtcNow }); sender.Status = IntegrationStatus.Connected;
        await db.SaveChangesAsync(ct); return Ok(new { sender.Id, sender.Name, sender.Status, verified = true });
    }

    [HttpPost("suppressions"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> Suppress(SuppressionInput input, CancellationToken ct)
    {
        var email = input.Email.Trim().ToLowerInvariant();
        var contact = await db.Contacts.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Email == email, ct);
        if (contact is null) { contact = Contact.Create(TenantId, null, "Suppressed", "Recipient", email, "", "subscriber"); db.Contacts.Add(contact); }
        var consent = await db.ConsentRecords.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.ContactId == contact.Id && x.Type == "marketing", ct);
        if (consent is null) { consent = new ConsentRecord { TenantId = TenantId, ContactId = contact.Id, Type = "marketing" }; db.ConsentRecords.Add(consent); }
        consent.Granted = false; consent.RecordedAtUtc = DateTime.UtcNow; consent.Source = string.IsNullOrWhiteSpace(input.Reason) ? "manual-suppression" : input.Reason.Trim();
        await db.SaveChangesAsync(ct); return Ok(new { email, suppressed = true });
    }

    [HttpPost("messages/{id:guid}/request-approval"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> RequestApproval(Guid id, CancellationToken ct)
    {
        if (!await db.OutreachMessages.AnyAsync(x => x.TenantId == TenantId && x.Id == id, ct)) return NotFound();
        var title = $"APPROVAL: Send outreach {id}";
        var task = await db.CrmTasks.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Title == title, ct);
        if (task is null) { task = new CrmTask { TenantId = TenantId, Title = title, DueAtUtc = DateTime.UtcNow.AddHours(4) }; db.CrmTasks.Add(task); await db.SaveChangesAsync(ct); }
        return Ok(task);
    }

    [HttpPost("messages/{id:guid}/approve-and-send"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> ApproveAndSend(Guid id, CancellationToken ct)
    {
        var task = await db.CrmTasks.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Title == $"APPROVAL: Send outreach {id}", ct);
        if (task is null) return BadRequest(new { detail = "Request approval before sending this message." });
        task.Completed = true; await db.SaveChangesAsync(ct);
        var result = await delivery.SendApprovedAsync(TenantId, id, ct);
        return result.Success ? Ok(result) : Conflict(new { detail = result.Error });
    }
}

public sealed record SenderInput(string Email, string Name, string Provider);
public sealed record VerifySenderInput(string Token);
public sealed record SuppressionInput(string Email, string Reason);
