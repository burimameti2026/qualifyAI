using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Email;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequirePermission(QualifyAiPermissions.SystemAdmin)]
[Route("api/admin/email-test")]
public sealed class AdminEmailTestController(
    AppDbContext db,
    ITenantContext tenant,
    BrevoEmailProvider brevo,
    IConfiguration configuration) : ControllerBase
{
    private const string SenderEmail = "fusionfleetmk@gmail.com";
    private const string SenderName = "TeamFusionFleet Mk";
    private const string TestRecipient = "fusionfleetmk@gmail.com";

    private Guid TenantId => tenant.TenantId();

    [HttpGet("sender")]
    public async Task<IActionResult> Sender(CancellationToken ct)
    {
        var configured = await db.IntegrationConnections.AsNoTracking()
            .Where(x => x.TenantId == TenantId &&
                        x.Provider == "email-sender" &&
                        x.Name == SenderEmail &&
                        x.Status == IntegrationStatus.Connected)
            .Select(x => new { x.Id, x.Status, x.SettingsJson })
            .FirstOrDefaultAsync(ct);

        var brevoSender = await brevo.FindSenderAsync(SenderEmail, ct);
        var connected = configured is not null && ReadVerified(configured.SettingsJson);
        var providerConfigured =
            !string.IsNullOrWhiteSpace(configuration["Email:Brevo:ApiKey"]) &&
            string.Equals(configuration["Email:Provider"] ?? "disabled", "brevo", StringComparison.OrdinalIgnoreCase);

        return Ok(new
        {
            provider = "brevo",
            providerConfigured,
            sender = SenderEmail,
            displayName = SenderName,
            recipient = TestRecipient,
            tenantSenderConfigured = configured is not null,
            tenantSenderVerified = connected,
            brevoVerified = brevoSender.Success && brevoSender.Verified,
            ready = providerConfigured && connected && brevoSender.Success && brevoSender.Verified,
            error = brevoSender.Success ? null : brevoSender.Error
        });
    }

    [HttpGet("prospects")]
    public async Task<IActionResult> Prospects(CancellationToken ct)
    {
        var rows = await db.Prospects
            .AsNoTracking()
            .Where(x =>
                x.TenantId == TenantId &&
                x.Status == ProspectStatus.Qualified &&
                !string.IsNullOrWhiteSpace(x.Email) &&
                !x.Email.EndsWith(".example"))
            .OrderByDescending(x => x.FitScore * 55 + x.IntentScore * 45)
            .Select(x => new
            {
                x.Id,
                x.CompanyName,
                x.ContactName,
                x.Email,
                x.Industry,
                x.Country,
                x.JobTitle,
                x.FitScore,
                x.IntentScore
            })
            .ToListAsync(ct);

        return Ok(rows.Select(x => new
        {
            x.Id,
            x.CompanyName,
            x.ContactName,
            x.Email,
            x.Industry,
            x.Country,
            x.JobTitle,
            PriorityScore = (int)Math.Round(x.FitScore * 0.55m + x.IntentScore * 0.45m)
        }));
    }

    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
    {
        var templates = await db.OutreachTemplates
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                subjectTemplate = x.SubjectTemplate,
                bodyTemplate = x.BodyTemplate
            })
            .ToListAsync(ct);

        return Ok(templates);
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] EmailTestInput input, CancellationToken ct)
    {
        if (!Guid.TryParse(input.ProspectId, out var prospectId) ||
            !Guid.TryParse(input.TemplateId, out var templateId))
            return BadRequest(new { detail = "A valid prospectId and templateId are required." });

        if (!string.Equals(input.RecipientEmail?.Trim(), TestRecipient, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { detail = $"Email test recipient must be {TestRecipient}." });

        var prospect = await db.Prospects.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == TenantId &&
                x.Id == prospectId &&
                x.Status == ProspectStatus.Qualified, ct);

        if (prospect is null)
            return NotFound(new { detail = "The selected prospect is not a qualified prospect for this tenant." });

        if (string.IsNullOrWhiteSpace(prospect.Email) || prospect.Email.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { detail = "The selected prospect does not have a deliverable email address." });

        var template = await db.OutreachTemplates.AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.TenantId == TenantId &&
                x.Id == templateId &&
                x.IsActive, ct);

        if (template is null)
            return NotFound(new { detail = "The selected outreach template was not found or is inactive." });

        var sender = await db.IntegrationConnections.AsNoTracking()
            .Where(x =>
                x.TenantId == TenantId &&
                x.Provider == "email-sender" &&
                x.Name == SenderEmail &&
                x.Status == IntegrationStatus.Connected)
            .Select(x => new { x.SettingsJson })
            .FirstOrDefaultAsync(ct);

        if (sender is null || !ReadVerified(sender.SettingsJson))
            return Conflict(new { detail = "The TeamFusionFleet Mk sender is not configured and verified for this tenant." });

        var brevoSender = await brevo.FindSenderAsync(SenderEmail, ct);
        if (!brevoSender.Success)
            return StatusCode(StatusCodes.Status502BadGateway, new { detail = brevoSender.Error });

        if (!brevoSender.Verified)
            return Conflict(new { detail = "fusionfleetmk@gmail.com is not active/verified in Brevo." });

        var subject = Render(template.SubjectTemplate, prospect);
        var body = Render(template.BodyTemplate, prospect);
        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            return BadRequest(new { detail = "The selected template rendered an empty subject or body." });

        var result = await brevo.SendAsync(new EmailEnvelope(
            SenderEmail,
            SenderName,
            TestRecipient,
            string.IsNullOrWhiteSpace(prospect.ContactName) ? prospect.CompanyName : prospect.ContactName,
            subject,
            WebUtility.HtmlEncode(body).Replace("\\n", "<br>"),
            body,
            $"email-test:{Guid.NewGuid():N}"), ct);

        if (!result.Success)
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                sent = false,
                provider = "brevo",
                error = result.Error
            });

        return Ok(new
        {
            sent = true,
            provider = "brevo",
            messageId = result.ProviderMessageId,
            sender = new { email = SenderEmail, name = SenderName },
            recipient = TestRecipient,
            prospectId = prospect.Id,
            templateId = template.Id,
            subject
        });
    }

    private static bool ReadVerified(string settingsJson)
    {
        try
        {
            using var json = JsonDocument.Parse(settingsJson);
            return json.RootElement.TryGetProperty("verified", out var value) && value.GetBoolean();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Render(string template, Prospect prospect)
    {
        return (template ?? string.Empty)
            .Replace("{{company}}", prospect.CompanyName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{contact}}", prospect.ContactName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{contactName}}", prospect.ContactName ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{industry}}", prospect.Industry ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{country}}", prospect.Country ?? string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("{{jobTitle}}", prospect.JobTitle ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record EmailTestInput(
    string ProspectId,
    string TemplateId,
    string RecipientEmail);
