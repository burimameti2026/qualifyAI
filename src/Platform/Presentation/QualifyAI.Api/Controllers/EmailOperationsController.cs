using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Infrastructure.Email;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController, Authorize, RequireModule(QualifyAiModules.Integrations)]
[Route("api/email-operations")]
public sealed class EmailOperationsController(
    AppDbContext db,
    ITenantContext tenant,
    EmailDeliveryService delivery,
    BrevoEmailProvider brevo,
    ProspectReplyProcessingService replyProcessor,
    IEnumerable<IEmailDeliveryProvider> emailProviders,
    IConfiguration configuration) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("senders"), RequirePermission(QualifyAiPermissions.IntegrationsRead)]
    public async Task<IActionResult> Senders(CancellationToken ct)
    {
        var senders = await db.IntegrationConnections.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.Provider == "email-sender")
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        return Ok(senders.Select(ToSenderResponse));
    }

    [HttpPost("senders"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> ConfigureSender(SenderInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Email) || !input.Email.Contains('@')) return BadRequest(new { detail = "A valid sender email is required." });
        var normalized = input.Email.Trim().ToLowerInvariant();
        var provider = input.Provider.Trim().ToLowerInvariant();
        if (provider is not ("brevo" or "smtp" or "sendgrid"))
            return BadRequest(new { detail = "Provider must be brevo, smtp or sendgrid." });

        var sender = await db.IntegrationConnections.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Provider == "email-sender" && x.Name == normalized, ct);
        if (sender is null) { sender = new IntegrationConnection { TenantId = TenantId, Provider = "email-sender", Name = normalized }; db.IntegrationConnections.Add(sender); }

        string? token = null;
        long? providerSenderId = null;
        var verified = false;
        var instruction = "Verify only after proving control of this mailbox/domain.";
        if (provider == "brevo")
        {
            var result = await brevo.EnsureSenderAsync(normalized, input.Name.Trim(), ct);
            if (!result.Success)
                return StatusCode(StatusCodes.Status502BadGateway, new { detail = result.Error });

            verified = result.Verified;
            providerSenderId = result.SenderId;
            instruction = verified
                ? "Sender is already active in Brevo."
                : "Open the Brevo verification email, confirm the sender, then click Check verification.";
        }
        else token = RandomNumberGenerator.GetHexString(16).ToLowerInvariant();

        sender.Status = verified ? IntegrationStatus.Connected : IntegrationStatus.Disconnected;
        sender.SettingsJson = JsonSerializer.Serialize(new
        {
            email = normalized,
            name = input.Name.Trim(),
            provider,
            verified,
            providerSenderId,
            verificationTokenHash = token is null ? null : HashVerificationToken(token)
        });
        sender.SecretReference = $"Email:{provider}:credentials";
        await db.SaveChangesAsync(ct);

        if (!verified && !provider.Equals("brevo", StringComparison.OrdinalIgnoreCase))
        {
            var sent = await SendVerificationEmailAsync(normalized, input.Name.Trim(), provider, token!, ct);
            instruction = sent.Success
                ? "A verification code was sent to this mailbox. Enter that code in Check verification."
                : $"Sender was saved but the verification email could not be sent: {sent.Error}. Configure and authenticate this sender/domain in {provider} first, then resend verification.";
        }

        return Ok(new { sender.Id, sender.Name, sender.Status, provider, verified, instruction });
    }

    [HttpPost("senders/{id:guid}/verify"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> VerifySender(Guid id, VerifySenderInput input, CancellationToken ct)
    {
        var sender = await db.IntegrationConnections.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.Provider == "email-sender", ct);
        if (sender is null) return NotFound();
        using var settings = JsonDocument.Parse(sender.SettingsJson);
        var root = settings.RootElement;
        var email = root.GetProperty("email").GetString() ?? "";
        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
        var provider = root.TryGetProperty("provider", out var p) ? p.GetString() ?? "smtp" : "smtp";
        long? providerSenderId = root.TryGetProperty("providerSenderId", out var senderId) &&
                                 senderId.ValueKind == JsonValueKind.Number &&
                                 senderId.TryGetInt64(out var value)
            ? value
            : null;

        if (provider.Equals("brevo", StringComparison.OrdinalIgnoreCase))
        {
            var result = await brevo.FindSenderAsync(email, ct);
            if (!result.Success)
                return StatusCode(StatusCodes.Status502BadGateway, new { detail = result.Error });
            if (!result.Verified)
                return Conflict(new { detail = "The sender is not active in Brevo yet. Confirm the verification email or authenticate the domain first." });
            providerSenderId = result.SenderId;
        }
        else if (!root.TryGetProperty("verificationTokenHash", out var expected) ||
                 !WebhookTokenMatches(expected.GetString() ?? string.Empty, HashVerificationToken(input.Token ?? string.Empty)))
        {
            return BadRequest(new { detail = "Verification token is invalid." });
        }

        sender.SettingsJson = JsonSerializer.Serialize(new { email, name, provider, verified = true, providerSenderId, verifiedAtUtc = DateTime.UtcNow });
        sender.Status = IntegrationStatus.Connected;
        await db.SaveChangesAsync(ct);
        return Ok(new { sender.Id, sender.Name, sender.Status, provider, verified = true });
    }

    [HttpPost("senders/{id:guid}/send-verification"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> SendVerification(Guid id, CancellationToken ct)
    {
        var sender = await db.IntegrationConnections.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id && x.Provider == "email-sender", ct);
        if (sender is null) return NotFound();
        using var settings = JsonDocument.Parse(sender.SettingsJson);
        var root = settings.RootElement;
        var provider = root.TryGetProperty("provider", out var p) ? p.GetString() ?? "smtp" : "smtp";
        if (provider.Equals("brevo", StringComparison.OrdinalIgnoreCase))
            return Conflict(new { detail = "Brevo sender verification is completed in Brevo. Use Check verification after confirming its email." });
        var email = root.TryGetProperty("email", out var e) ? e.GetString() ?? string.Empty : string.Empty;
        var name = root.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        var providerSenderId = root.TryGetProperty("providerSenderId", out var senderId) && senderId.TryGetInt64(out var value) ? value : (long?)null;
        var token = RandomNumberGenerator.GetHexString(16).ToLowerInvariant();
        sender.SettingsJson = JsonSerializer.Serialize(new
        {
            email,
            name,
            provider,
            verified = false,
            providerSenderId,
            verificationTokenHash = HashVerificationToken(token)
        });
        await db.SaveChangesAsync(ct);
        var result = await SendVerificationEmailAsync(email, name, provider, token, ct);
        return result.Success ? Ok(new { sent = true }) : Conflict(new { detail = result.Error });
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

    [AllowAnonymous]
    [HttpPost("webhooks/brevo")]
    public async Task<IActionResult> BrevoWebhook([FromBody] JsonElement payload, [FromHeader(Name = "X-QualifyAI-Webhook-Token")] string? token, [FromQuery] string? accessToken, CancellationToken ct)
    {
        var expectedToken = configuration["Email:Brevo:WebhookToken"];
        if (string.IsNullOrWhiteSpace(expectedToken))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { detail = "Brevo webhook token is not configured." });
        var suppliedToken = string.IsNullOrWhiteSpace(token) ? accessToken : token;
        if (string.IsNullOrWhiteSpace(suppliedToken) || !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(suppliedToken), Encoding.UTF8.GetBytes(expectedToken)))
            return Unauthorized();

        var events = payload.ValueKind == JsonValueKind.Array ? payload.EnumerateArray().ToArray() : [payload];
        var processed = 0;
        foreach (var item in events)
        {
            var eventName = ReadString(item, "event").ToLowerInvariant();
            var providerMessageId = ReadString(item, "message-id");
            if (string.IsNullOrWhiteSpace(providerMessageId)) providerMessageId = ReadString(item, "messageId");
            var email = ReadString(item, "email").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(providerMessageId)) continue;
            var eventId = ReadString(item, "ts_event");
            if (string.IsNullOrWhiteSpace(eventId)) eventId = HashEvent(item.GetRawText());
            var message = await FindMessageAsync(string.Empty, providerMessageId, ct);
            if (message is not null && await ApplyProviderEventAsync(message, "brevo", eventName, eventId, email, item.GetRawText(), ct))
                processed++;
        }
        await db.SaveChangesAsync(ct);
        return Ok(new { processed });
    }

    [AllowAnonymous]
    [HttpPost("webhooks/sendgrid")]
    public async Task<IActionResult> SendGridWebhook(
        [FromBody] JsonElement payload,
        [FromHeader(Name = "X-QualifyAI-Webhook-Token")] string? token,
        [FromQuery] string? accessToken,
        CancellationToken ct)
    {
        var expectedToken = configuration["Email:SendGrid:WebhookToken"];
        if (string.IsNullOrWhiteSpace(expectedToken))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { detail = "SendGrid webhook token is not configured." });
        if (!WebhookTokenMatches(expectedToken, string.IsNullOrWhiteSpace(token) ? accessToken : token))
            return Unauthorized();

        var events = payload.ValueKind == JsonValueKind.Array ? payload.EnumerateArray().ToArray() : [payload];
        var processed = 0;
        foreach (var item in events)
        {
            var eventName = ReadString(item, "event").ToLowerInvariant();
            var providerMessageId = ReadString(item, "sg_message_id");
            if (string.IsNullOrWhiteSpace(providerMessageId)) providerMessageId = ReadString(item, "message_id");
            var correlationId = ReadString(item, "qualifyai_message_id");
            var message = await FindMessageAsync(correlationId, providerMessageId, ct);
            if (message is null) continue;

            var email = ReadString(item, "email").Trim().ToLowerInvariant();
            var eventId = ReadString(item, "sg_event_id");
            if (string.IsNullOrWhiteSpace(eventId)) eventId = HashEvent(item.GetRawText());
            if (await ApplyProviderEventAsync(message, "sendgrid", eventName, eventId, email, item.GetRawText(), ct))
                processed++;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { processed });
    }

    [AllowAnonymous]
    [HttpPost("webhooks/sendgrid/inbound")]
    [Consumes("multipart/form-data", "application/x-www-form-urlencoded")]
    public async Task<IActionResult> SendGridInbound(
        [FromForm] SendGridInboundInput input,
        [FromHeader(Name = "X-QualifyAI-Webhook-Token")] string? token,
        [FromQuery] string? accessToken,
        CancellationToken ct)
    {
        var expectedToken = configuration["Email:SendGrid:InboundWebhookToken"];
        if (string.IsNullOrWhiteSpace(expectedToken))
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { detail = "SendGrid inbound webhook token is not configured." });
        if (!WebhookTokenMatches(expectedToken, string.IsNullOrWhiteSpace(token) ? accessToken : token))
            return Unauthorized();

        var fromEmail = ExtractEmail(input.From);
        if (string.IsNullOrWhiteSpace(fromEmail))
            return BadRequest(new { detail = "Inbound reply does not include a valid From address." });

        var message = await FindInboundMessageAsync(input.Headers, fromEmail, ExtractEmail(input.To), ct);
        if (message is null)
            return Accepted(new { processed = false, detail = "No unambiguous outreach message matched this inbound reply." });

        var body = string.IsNullOrWhiteSpace(input.Text) ? input.Html : input.Text;
        var classification = ProspectReplyProcessingService.NormalizeClassification(null, body);
        var result = await replyProcessor.ProcessAsync(message.TenantId, new ProcessProspectReplyRequest(
            message.CampaignId, message.ProspectId, message.Id, body, classification,
            classification == "interested" ? 90 : classification is "unsubscribe" or "not-interested" ? -90 : 0,
            classification is "unclassified" or "auto-reply"), ct);

        return result is null
            ? Accepted(new { processed = false, detail = "The matched campaign recipient no longer exists." })
            : Ok(new { processed = true, result.Classification, result.Interested, result.Suppressed, result.NextAction });
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

    [HttpPost("messages/{id:guid}/retry"), RequirePermission(QualifyAiPermissions.IntegrationsManage)]
    public async Task<IActionResult> RetrySend(Guid id, CancellationToken ct)
    {
        var message = await db.OutreachMessages.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (message is null) return NotFound();
        if (message.Status != OutreachStatus.Failed)
            return Conflict(new { detail = "Only failed outreach messages can be retried." });
        message.Status = OutreachStatus.Queued;
        await db.SaveChangesAsync(ct);
        var result = await delivery.SendApprovedAsync(TenantId, id, ct);
        return result.Success ? Ok(result) : Conflict(new { detail = result.Error });
    }

    private async Task<OutreachMessage?> FindMessageAsync(string correlationId, string providerMessageId, CancellationToken ct)
    {
        if (Guid.TryParse(correlationId, out var messageId))
        {
            var byId = await db.OutreachMessages.FirstOrDefaultAsync(x => x.Id == messageId, ct);
            if (byId is not null) return byId;
        }

        return string.IsNullOrWhiteSpace(providerMessageId)
            ? null
            : await db.OutreachMessages.FirstOrDefaultAsync(x => x.ProviderMessageId == providerMessageId, ct);
    }

    private async Task<OutreachMessage?> FindInboundMessageAsync(string? headers, string fromEmail, string? toEmail, CancellationToken ct)
    {
        var correlationId = HeaderValue(headers, "X-QualifyAI-Message-Id");
        var providerMessageId = HeaderValue(headers, "In-Reply-To").Trim('<', '>', ' ');
        var direct = await FindMessageAsync(correlationId, providerMessageId, ct);
        if (direct is not null)
        {
            var directProspect = await db.Prospects.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == direct.TenantId && x.Id == direct.ProspectId, ct);
            if (directProspect?.Email.Equals(fromEmail, StringComparison.OrdinalIgnoreCase) == true)
                return direct;
        }

        var candidates = from message in db.OutreachMessages.AsNoTracking()
                         join prospect in db.Prospects.AsNoTracking() on message.ProspectId equals prospect.Id
                         join campaign in db.Campaigns.AsNoTracking() on message.CampaignId equals campaign.Id
                         where message.Status == OutreachStatus.Sent || message.Status == OutreachStatus.Delivered
                         where prospect.Email == fromEmail
                         select new { Message = message, campaign.SenderEmail };
        if (!string.IsNullOrWhiteSpace(toEmail))
            candidates = candidates.Where(x => x.SenderEmail == toEmail);

        var matches = await candidates.OrderByDescending(x => x.Message.SentAtUtc).Take(2).ToListAsync(ct);
        return matches.Count == 1 ? matches[0].Message : null;
    }

    private async Task<bool> ApplyProviderEventAsync(
        OutreachMessage message,
        string provider,
        string eventName,
        string eventId,
        string email,
        string payload,
        CancellationToken ct)
    {
        var normalizedEvent = eventName.Trim().ToLowerInvariant();
        var dedupeKey = $"{provider}:{message.Id:N}:{normalizedEvent}:{eventId}";
        if (await db.AuditLogs.AnyAsync(x => x.TenantId == message.TenantId && x.Action == "email.provider.event" && x.EntityId == dedupeKey, ct))
            return false;

        var recipient = await db.CampaignRecipients.FirstOrDefaultAsync(x =>
            x.TenantId == message.TenantId && x.CampaignId == message.CampaignId && x.ProspectId == message.ProspectId, ct);
        var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId == message.TenantId && x.Id == message.ProspectId, ct);

        switch (normalizedEvent)
        {
            case "delivered":
                if (message.Status is not OutreachStatus.Replied and not OutreachStatus.Suppressed)
                    message.Status = OutreachStatus.Delivered;
                break;
            case "hard_bounce":
            case "bounce":
            case "dropped":
                if (message.Status is not OutreachStatus.Replied and not OutreachStatus.Suppressed)
                    message.Status = OutreachStatus.Failed;
                if (recipient is not null) { recipient.Status = "failed"; recipient.NextRunAtUtc = null; }
                if (prospect is not null) prospect.Status = ProspectStatus.Suppressed;
                if (!string.IsNullOrWhiteSpace(email)) await SuppressEmailAsync(message.TenantId, email, $"{provider}-{normalizedEvent}", ct);
                await StopQueuedFollowUpsAsync(message, ct);
                break;
            case "soft_bounce":
            case "blocked":
            case "deferred":
            case "error":
                if (message.Status is not OutreachStatus.Replied and not OutreachStatus.Suppressed)
                    message.Status = OutreachStatus.Failed;
                if (recipient is not null) { recipient.Status = "failed"; recipient.NextRunAtUtc = null; }
                await StopQueuedFollowUpsAsync(message, ct);
                break;
            case "unsubscribed":
            case "unsubscribe":
            case "group_unsubscribe":
            case "spam":
            case "spamreport":
                message.Status = OutreachStatus.Suppressed;
                if (recipient is not null) { recipient.Status = "suppressed"; recipient.NextRunAtUtc = null; }
                if (prospect is not null) prospect.Status = ProspectStatus.Suppressed;
                if (!string.IsNullOrWhiteSpace(email)) await SuppressEmailAsync(message.TenantId, email, $"{provider}-{normalizedEvent}", ct);
                await StopQueuedFollowUpsAsync(message, ct);
                break;
            default:
                return false;
        }

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = message.TenantId,
            Action = "email.provider.event",
            EntityType = nameof(OutreachMessage),
            EntityId = dedupeKey,
            DataJson = payload
        });
        return true;
    }

    private async Task StopQueuedFollowUpsAsync(OutreachMessage message, CancellationToken ct)
    {
        var queuedFollowUps = await db.OutreachMessages.Where(x =>
                x.TenantId == message.TenantId && x.CampaignId == message.CampaignId && x.ProspectId == message.ProspectId &&
                x.Id != message.Id && x.Status == OutreachStatus.Queued)
            .ToListAsync(ct);
        foreach (var followUp in queuedFollowUps)
            followUp.Status = OutreachStatus.Suppressed;
    }

    private static bool WebhookTokenMatches(string expectedToken, string? suppliedToken) =>
        !string.IsNullOrWhiteSpace(suppliedToken) &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(suppliedToken), Encoding.UTF8.GetBytes(expectedToken));

    private static string HashEvent(string payload) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();

    private static string HashVerificationToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    private static string HeaderValue(string? headers, string name)
    {
        if (string.IsNullOrWhiteSpace(headers)) return string.Empty;
        var prefix = name + ":";
        return headers.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))?
            .Substring(prefix.Length).Trim() ?? string.Empty;
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

    private async Task<EmailProviderResult> SendVerificationEmailAsync(
        string email,
        string name,
        string providerName,
        string token,
        CancellationToken ct)
    {
        var provider = emailProviders.FirstOrDefault(x => x.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
            return new EmailProviderResult(false, null, $"Email provider '{providerName}' is not enabled.");
        if (string.IsNullOrWhiteSpace(email))
            return new EmailProviderResult(false, null, "Sender email is missing.");

        return await provider.SendAsync(new EmailEnvelope(
            email, string.IsNullOrWhiteSpace(name) ? "QualifyAI" : name,
            email, string.IsNullOrWhiteSpace(name) ? "Sender" : name,
            "Confirm your QualifyAI sending identity",
            $"<p>Enter this verification code in QualifyAI:</p><p><strong>{token}</strong></p><p>If you did not request this, ignore this email.</p>",
            $"Enter this verification code in QualifyAI: {token}",
            null), ct);
    }

    private static object ToSenderResponse(IntegrationConnection sender)
    {
        try
        {
            using var settings = JsonDocument.Parse(sender.SettingsJson);
            var root = settings.RootElement;
            return new
            {
                sender.Id,
                sender.Name,
                sender.Status,
                email = root.TryGetProperty("email", out var email) ? email.GetString() : sender.Name,
                displayName = root.TryGetProperty("name", out var name) ? name.GetString() : "",
                provider = root.TryGetProperty("provider", out var provider) ? provider.GetString() : "smtp",
                verified = root.TryGetProperty("verified", out var verified) && verified.GetBoolean(),
                sender.CreatedAtUtc
            };
        }
        catch (JsonException)
        {
            return new { sender.Id, sender.Name, sender.Status, email = sender.Name, displayName = "", provider = "unknown", verified = false, sender.CreatedAtUtc };
        }
    }

    private static string ReadString(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) ? value.ToString() : string.Empty;

    private async Task SuppressEmailAsync(Guid tenantId, string email, string reason, CancellationToken ct)
    {
        var contact = await db.Contacts.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Email == email, ct);
        if (contact is null)
        {
            contact = Contact.Create(tenantId, null, "Suppressed", "Recipient", email, string.Empty, "subscriber");
            db.Contacts.Add(contact);
        }
        var consent = await db.ConsentRecords.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.ContactId == contact.Id && x.Type == "marketing", ct);
        if (consent is null)
        {
            consent = new ConsentRecord { TenantId = tenantId, ContactId = contact.Id, Type = "marketing" };
            db.ConsentRecords.Add(consent);
        }
        consent.Granted = false;
        consent.RecordedAtUtc = DateTime.UtcNow;
        consent.Source = reason;
    }
}

public sealed record SenderInput(string Email, string Name, string Provider);
public sealed record VerifySenderInput(string? Token);
public sealed record SuppressionInput(string Email, string Reason);
public sealed class SendGridInboundInput
{
    public string? From { get; set; }
    public string? To { get; set; }
    public string? Subject { get; set; }
    public string? Text { get; set; }
    public string? Html { get; set; }
    public string? Headers { get; set; }
}
