using System.Text.Json;
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

[ApiController]
[Authorize]
[RequirePermission(QualifyAiPermissions.SystemAdmin)]
[Route("api/admin/email-test")]
public sealed class AdminEmailTestController(
    AppDbContext db,
    ITenantContext tenant,
    IEnumerable<IEmailDeliveryProvider> emailProviders,
    IConfiguration configuration) : ControllerBase
{
    private const string ExpectedSenderEmail = "fusionfleetmk@gmail.com";
    private const string ExpectedSenderName = "TeamFusionFleet Mk";

    private Guid TenantId => tenant.TenantId();

    [HttpGet("sender")]
    public async Task<IActionResult> Sender(CancellationToken ct)
    {
        var sender = await FindVerifiedSenderAsync(ct);
        if (sender is null)
            return NotFound(new { detail = "No verified Brevo sender is configured for this tenant." });

        return Ok(new
        {
            email = sender.Email,
            name = sender.Name,
            provider = sender.Provider,
            verified = sender.Verified
        });
    }

    [HttpGet("prospects")]
    public async Task<IActionResult> Prospects(CancellationToken ct)
    {
        var prospects = await db.Prospects.AsNoTracking()
            .Where(x => x.TenantId == TenantId &&
                        x.Status == ProspectStatus.Qualified &&
                        !string.IsNullOrWhiteSpace(x.Email) &&
                        !x.Email.EndsWith(".example"))
            .OrderByDescending(x => x.PriorityScore)
            .Select(x => new
            {
                x.Id,
                x.CompanyName,
                x.ContactName,
                x.Email,
                x.Industry,
                x.Country,
                x.JobTitle,
                x.PriorityScore
            })
            .ToListAsync(ct);

        return Ok(prospects);
    }

    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
    {
        var templates = await db.OutreachTemplates.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.IsActive)
            .OrderBy(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.SubjectTemplate,
                x.BodyTemplate
            })
            .ToListAsync(ct);

        return Ok(templates);
    }

    [HttpPost("send")]
    public async Task<IActionResult> Send(TestEmailInput input, CancellationToken ct)
    {
        var recipientEmail = input.RecipientEmail.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains('@'))
            return BadRequest(new { detail = "A valid test recipient email is required." });

        var sender = await FindVerifiedSenderAsync(ct);
        if (sender is null)
            return Conflict(new { detail = "Configure and verify the Brevo sender before sending a test email." });

        if (!sender.Email.Equals(ExpectedSenderEmail, StringComparison.OrdinalIgnoreCase))
            return Conflict(new { detail = $"The Email Test Center is locked to {ExpectedSenderEmail} for the FusionFleet test." });

        if (!recipientEmail.Equals(sender.Email, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { detail = "For this test, the recipient must be the verified sender mailbox." });

        var prospect = await db.Prospects.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == input.ProspectId && x.Status == ProspectStatus.Qualified,
            ct);
        if (prospect is null)
            return BadRequest(new { detail = "The selected prospect is not a qualified prospect in this tenant." });

        var template = await db.OutreachTemplates.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == input.TemplateId && x.IsActive,
            ct);
        if (template is null)
            return BadRequest(new { detail = "The selected outreach template was not found in this tenant." });

        var providerName = configuration["Email:Provider"]?.Trim().ToLowerInvariant();
        if (!string.Equals(providerName, "brevo", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { detail = "Email Test Center requires Email:Provider=brevo in the API environment." });

        var provider = emailProviders.FirstOrDefault(x => x.Name.Equals("brevo", StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { detail = "The Brevo email provider is not registered." });

        var subject = CampaignExecutionService.RenderTemplate(template.SubjectTemplate, prospect);
        var body = CampaignExecutionService.RenderTemplate(template.BodyTemplate, prospect);

        var result = await provider.SendAsync(new EmailEnvelope(
            sender.Email,
            string.IsNullOrWhiteSpace(sender.Name) ? ExpectedSenderName : sender.Name,
            recipientEmail,
            "FusionFleet test mailbox",
            subject,
            body.Replace("\n", "<br>"),
            body,
            $"email-test:{Guid.NewGuid():N}"), ct);

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId,
            Action = result.Success ? "email.test.sent" : "email.test.failed",
            EntityType = nameof(OutreachTemplate),
            EntityId = template.Id.ToString(),
            DataJson = JsonSerializer.Serialize(new
            {
                provider = "brevo",
                sender = sender.Email,
                recipient = recipientEmail,
                prospectId = prospect.Id,
                templateId = template.Id,
                providerMessageId = result.ProviderMessageId,
                error = result.Error
            })
        });
        await db.SaveChangesAsync(ct);

        if (!result.Success)
            return Conflict(new { detail = result.Error ?? "Brevo rejected the test email." });

        return Ok(new
        {
            sent = true,
            provider = "brevo",
            providerMessageId = result.ProviderMessageId,
            sender = sender.Email,
            recipient = recipientEmail,
            prospect = prospect.CompanyName,
            template = template.Name,
            subject
        });
    }

    private async Task<ConfiguredSender?> FindVerifiedSenderAsync(CancellationToken ct)
    {
        var connections = await db.IntegrationConnections.AsNoTracking()
            .Where(x => x.TenantId == TenantId &&
                        x.Provider == "email-sender" &&
                        x.Status == IntegrationStatus.Connected)
            .ToListAsync(ct);

        foreach (var connection in connections)
        {
            try
            {
                using var json = JsonDocument.Parse(connection.SettingsJson);
                var root = json.RootElement;
                var email = root.TryGetProperty("email", out var emailValue) ? emailValue.GetString() : null;
                var name = root.TryGetProperty("name", out var nameValue) ? nameValue.GetString() : null;
                var provider = root.TryGetProperty("provider", out var providerValue) ? providerValue.GetString() : null;
                var verified = root.TryGetProperty("verified", out var verifiedValue) && verifiedValue.GetBoolean();

                if (!string.IsNullOrWhiteSpace(email) &&
                    verified &&
                    string.Equals(provider, "brevo", StringComparison.OrdinalIgnoreCase))
                    return new ConfiguredSender(email.Trim(), name?.Trim() ?? string.Empty, provider!, true);
            }
            catch (JsonException)
            {
                // Ignore malformed sender settings and inspect the next connection.
            }
        }

        return null;
    }

    private sealed record ConfiguredSender(string Email, string Name, string Provider, bool Verified);
}

public sealed record TestEmailInput(Guid ProspectId, Guid TemplateId, string RecipientEmail);
