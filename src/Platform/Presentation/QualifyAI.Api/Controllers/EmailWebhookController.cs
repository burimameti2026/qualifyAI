using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/email-operations/webhooks")]
public sealed class EmailWebhookController(
    AppDbContext db,
    ProspectReplyProcessingService replyProcessor,
    IConfiguration configuration) : ControllerBase
{
    [HttpPost("sendgrid")]
    public async Task<IActionResult> SendGrid([FromBody] JsonElement payload, [FromHeader(Name = "X-QualifyAI-Webhook-Token")] string? token, [FromQuery] string? accessToken, CancellationToken ct)
    {
        if (!TokenMatches(configuration["Email:SendGrid:WebhookToken"], string.IsNullOrWhiteSpace(token) ? accessToken : token))
            return Unauthorized();

        var events = payload.ValueKind == JsonValueKind.Array ? payload.EnumerateArray().ToArray() : new[] { payload };
        var processed = 0;
        foreach (var item in events)
        {
            var eventName = Read(item, "event").ToLowerInvariant();
            var providerMessageId = Read(item, "sg_message_id");
            if (string.IsNullOrWhiteSpace(providerMessageId)) providerMessageId = Read(item, "message_id");
            var correlationId = Read(item, "qualifyai_message_id");
            if (string.IsNullOrWhiteSpace(correlationId)) correlationId = Read(item, "leadsai_message_id");
            var message = await FindMessageAsync(correlationId, providerMessageId, ct);
            if (message is null) continue;

            switch (eventName)
            {
                case "delivered":
                    message.Status = OutreachStatus.Delivered;
                    processed++;
                    break;
                case "bounce":
                case "hard_bounce":
                case "dropped":
                    message.Status = OutreachStatus.Failed;
                    await SuppressAndStopAsync(message, Read(item, "email"), $"sendgrid-{eventName}", ct);
                    processed++;
                    break;
                case "unsubscribe":
                case "group_unsubscribe":
                case "spamreport":
                case "spam":
                    message.Status = OutreachStatus.Suppressed;
                    await SuppressAndStopAsync(message, Read(item, "email"), $"sendgrid-{eventName}", ct);
                    processed++;
                    break;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { processed });
    }

    [HttpPost("sendgrid/inbound")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<IActionResult> SendGridInbound([FromForm] SendGridInboundInput input, [FromHeader(Name = "X-QualifyAI-Webhook-Token")] string? token, [FromQuery] string? accessToken, CancellationToken ct)
    {
        if (!TokenMatches(configuration["Email:SendGrid:InboundWebhookToken"], string.IsNullOrWhiteSpace(token) ? accessToken : token))
            return Unauthorized();

        var fromEmail = ExtractEmail(input.From);
        if (string.IsNullOrWhiteSpace(fromEmail))
            return BadRequest(new { detail = "Inbound reply does not include a valid From address." });

        var messageId = HeaderValue(input.Headers, "X-LeadsAI-Message-Id");
        if (string.IsNullOrWhiteSpace(messageId)) messageId = HeaderValue(input.Headers, "X-QualifyAI-Message-Id");
        var providerMessageId = HeaderValue(input.Headers, "In-Reply-To").Trim('<', '>', ' ');
        var message = await FindMessageAsync(messageId, providerMessageId, ct);

        if (message is null)
        {
            var candidates = await (from outreach in db.OutreachMessages
                                    join prospect in db.Prospects on outreach.ProspectId equals prospect.Id
                                    join campaign in db.Campaigns on outreach.CampaignId equals campaign.Id
                                    where prospect.Email == fromEmail &&
                                          (outreach.Status == OutreachStatus.Sent || outreach.Status == OutreachStatus.Delivered)
                                    select new { Message = outreach, campaign.SenderEmail })
                .Where(x => string.IsNullOrWhiteSpace(input.To) || x.SenderEmail == ExtractEmail(input.To))
                .OrderByDescending(x => x.Message.SentAtUtc)
                .Take(2)
                .ToListAsync(ct);
            message = candidates.Count == 1 ? candidates[0].Message : null;
        }

        if (message is null)
            return Accepted(new { processed = false, detail = "No unambiguous outreach message matched this reply." });

        var body = string.IsNullOrWhiteSpace(input.Text) ? input.Html : input.Text;
        var classification = ProspectReplyProcessingService.NormalizeClassification(null, body);
        var result = await replyProcessor.ProcessAsync(message.TenantId, new ProcessProspectReplyRequest(
            message.CampaignId,
            message.ProspectId,
            message.Id,
            body,
            classification,
            classification == "interested" ? 90 : classification is "unsubscribe" or "not-interested" ? -90 : 0,
            classification is "unclassified" or "auto-reply"), ct);

        return result is null
            ? Accepted(new { processed = false, detail = "The matched campaign recipient no longer exists." })
            : Ok(new { processed = true, result.Classification, result.Interested, result.Suppressed, result.NextAction });
    }

    private async Task<OutreachMessage?> FindMessageAsync(string correlationId, string providerMessageId, CancellationToken ct)
    {
        if (Guid.TryParse(correlationId, out var id))
        {
            var byId = await db.OutreachMessages.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (byId is not null) return byId;
        }

        return string.IsNullOrWhiteSpace(providerMessageId)
            ? null
            : await db.OutreachMessages.FirstOrDefaultAsync(x => x.ProviderMessageId == providerMessageId, ct);
    }

    private async Task SuppressAndStopAsync(OutreachMessage message, string email, string reason, CancellationToken ct)
    {
        var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId == message.TenantId && x.Id == message.ProspectId, ct);
        if (prospect is not null) prospect.Status = ProspectStatus.Suppressed;

        var contactId = prospect?.ContactId ?? await db.Contacts.Where(x => x.TenantId == message.TenantId && x.Email == email).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (contactId.HasValue)
        {
            var consent = await db.ConsentRecords.FirstOrDefaultAsync(x => x.TenantId == message.TenantId && x.ContactId == contactId && x.Type == "marketing", ct);
            if (consent is null)
            {
                consent = new ConsentRecord { TenantId = message.TenantId, ContactId = contactId.Value, Type = "marketing" };
                db.ConsentRecords.Add(consent);
            }
            consent.Granted = false;
            consent.RecordedAtUtc = DateTime.UtcNow;
            consent.Source = reason;
        }

        var recipient = await db.CampaignRecipients.FirstOrDefaultAsync(x => x.TenantId == message.TenantId && x.CampaignId == message.CampaignId && x.ProspectId == message.ProspectId, ct);
        if (recipient is not null)
        {
            recipient.Status = "suppressed";
            recipient.NextRunAtUtc = null;
        }

        var queued = await db.OutreachMessages.Where(x => x.TenantId == message.TenantId && x.CampaignId == message.CampaignId && x.ProspectId == message.ProspectId && x.Id != message.Id && x.Status == OutreachStatus.Queued).ToListAsync(ct);
        foreach (var followUp in queued) followUp.Status = OutreachStatus.Suppressed;
    }

    private static bool TokenMatches(string? expected, string? supplied) =>
        !string.IsNullOrWhiteSpace(expected) &&
        !string.IsNullOrWhiteSpace(supplied) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied));

    private static string Read(JsonElement element, string name) => element.TryGetProperty(name, out var value) ? value.ToString() : string.Empty;

    private static string HeaderValue(string? headers, string name)
    {
        if (string.IsNullOrWhiteSpace(headers)) return string.Empty;
        var prefix = name + ":";
        return headers.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?.Substring(prefix.Length).Trim() ?? string.Empty;
    }

    private static string ExtractEmail(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var trimmed = value.Trim();
        var open = trimmed.LastIndexOf('<');
        var close = trimmed.LastIndexOf('>');
        if (open >= 0 && close > open) trimmed = trimmed[(open + 1)..close];
        return trimmed.Contains('@') ? trimmed.Trim().ToLowerInvariant() : string.Empty;
    }
}

public sealed record SendGridInboundInput(
    string? From,
    string? To,
    string? Headers,
    string? Text,
    string? Html);
