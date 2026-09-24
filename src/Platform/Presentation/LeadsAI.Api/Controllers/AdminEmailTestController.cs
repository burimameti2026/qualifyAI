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
        var tenantId = TenantId;
        var templates = await db.OutreachTemplates
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        if (templates.Count == 0)
        {
            templates = CreateDefaultTemplates(tenantId);
            db.OutreachTemplates.AddRange(templates);
            await db.SaveChangesAsync(ct);
        }

        return Ok(templates.Select(x => new
        {
            x.Id,
            x.Name,
            x.Description,
            x.SubjectTemplate,
            x.BodyTemplate
        }));
    }

    [HttpPost("templates")]
    public async Task<IActionResult> CreateTemplate(OutreachTemplateInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) ||
            string.IsNullOrWhiteSpace(input.SubjectTemplate) ||
            string.IsNullOrWhiteSpace(input.BodyTemplate))
            return BadRequest(new { detail = "Template name, subject and body are required." });

        var name = input.Name.Trim();
        if (await db.OutreachTemplates.AnyAsync(x => x.TenantId == TenantId && x.Name == name, ct))
            return Conflict(new { detail = "A template with this name already exists." });

        var template = new OutreachTemplate
        {
            TenantId = TenantId,
            Name = name,
            Description = input.Description?.Trim() ?? string.Empty,
            SubjectTemplate = input.SubjectTemplate.Trim(),
            BodyTemplate = input.BodyTemplate.Trim(),
            IsActive = true
        };

        db.OutreachTemplates.Add(template);
        await db.SaveChangesAsync(ct);
        return Created($"/api/admin/email-test/templates/{template.Id}", template);
    }

    [HttpPut("templates/{id:guid}")]
    public async Task<IActionResult> UpdateTemplate(Guid id, OutreachTemplateInput input, CancellationToken ct)
    {
        var template = await db.OutreachTemplates
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);

        if (template is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(input.Name) ||
            string.IsNullOrWhiteSpace(input.SubjectTemplate) ||
            string.IsNullOrWhiteSpace(input.BodyTemplate))
            return BadRequest(new { detail = "Template name, subject and body are required." });

        var name = input.Name.Trim();
        if (await db.OutreachTemplates.AnyAsync(
                x => x.TenantId == TenantId && x.Id != id && x.Name == name, ct))
            return Conflict(new { detail = "A template with this name already exists." });

        template.Name = name;
        template.Description = input.Description?.Trim() ?? string.Empty;
        template.SubjectTemplate = input.SubjectTemplate.Trim();
        template.BodyTemplate = input.BodyTemplate.Trim();
        template.IsActive = true;

        await db.SaveChangesAsync(ct);
        return Ok(template);
    }

    private static List<OutreachTemplate> CreateDefaultTemplates(Guid tenantId) =>
    [
        new OutreachTemplate
        {
            TenantId = tenantId,
            Name = "Logistics operational benchmark",
            Description = "Message 1 — initial outreach",
            SubjectTemplate = "{{company}}: reduce dispatch and delivery exceptions",
            BodyTemplate = "Hi {{contact}}, I noticed current growth signals at {{company}}. We help {{industry}} teams automate dispatch, warehouse and customer operations. Would a 25-minute operational demo be useful?"
        },
        new OutreachTemplate
        {
            TenantId = tenantId,
            Name = "Operational benchmark follow-up",
            Description = "Message 2 — follow-up",
            SubjectTemplate = "Operational benchmark for {{company}}",
            BodyTemplate = "Hi {{contact}}, I prepared a short benchmark for teams operating across {{country}}. I can tailor the demo to your fleet, warehouse and delivery workflow."
        },
        new OutreachTemplate
        {
            TenantId = tenantId,
            Name = "Close the loop",
            Description = "Message 3 — final follow-up",
            SubjectTemplate = "Should I close the loop on {{company}}?",
            BodyTemplate = "Hi {{contact}}, I don't want to keep filling your inbox if this isn't a priority. If improving dispatch, warehouse or delivery operations is on your roadmap, I'm happy to send a short example. Otherwise, I'll close the loop here."
        }
    ];

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

