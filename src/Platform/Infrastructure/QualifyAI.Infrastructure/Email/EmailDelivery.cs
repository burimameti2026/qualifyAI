using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Email;

public sealed record EmailEnvelope(string FromEmail, string FromName, string ToEmail, string ToName, string Subject, string HtmlBody, string TextBody);
public sealed record EmailProviderResult(bool Success, string? ProviderMessageId, string? Error = null);
public interface IEmailDeliveryProvider { string Name { get; } Task<EmailProviderResult> SendAsync(EmailEnvelope message, CancellationToken ct = default); }

public sealed class SmtpEmailProvider(IConfiguration configuration) : IEmailDeliveryProvider
{
    public string Name => "smtp";
    public async Task<EmailProviderResult> SendAsync(EmailEnvelope message, CancellationToken ct = default)
    {
        var host = configuration["Email:Smtp:Host"];
        if (string.IsNullOrWhiteSpace(host)) return new(false, null, "Email:Smtp:Host is not configured.");
        using var client = new SmtpClient(host, configuration.GetValue("Email:Smtp:Port", 587))
        {
            EnableSsl = configuration.GetValue("Email:Smtp:UseTls", true),
            Credentials = new NetworkCredential(configuration["Email:Smtp:Username"], configuration["Email:Smtp:Password"])
        };
        using var mail = new MailMessage { From = new MailAddress(message.FromEmail, message.FromName), Subject = message.Subject, Body = message.HtmlBody, IsBodyHtml = true };
        mail.To.Add(new MailAddress(message.ToEmail, message.ToName));
        try { await client.SendMailAsync(mail, ct); return new(true, $"smtp:{Guid.NewGuid():N}"); }
        catch (Exception exception) { return new(false, null, exception.Message); }
    }
}

public sealed class SendGridEmailProvider(HttpClient http, IConfiguration configuration) : IEmailDeliveryProvider
{
    public string Name => "sendgrid";
    public async Task<EmailProviderResult> SendAsync(EmailEnvelope message, CancellationToken ct = default)
    {
        var key = configuration["Email:SendGrid:ApiKey"];
        if (string.IsNullOrWhiteSpace(key)) return new(false, null, "Email:SendGrid:ApiKey is not configured.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        request.Content = new StringContent(JsonSerializer.Serialize(new
        {
            personalizations = new[] { new { to = new[] { new { email = message.ToEmail, name = message.ToName } } } },
            from = new { email = message.FromEmail, name = message.FromName },
            subject = message.Subject,
            content = new[] { new { type = "text/plain", value = message.TextBody }, new { type = "text/html", value = message.HtmlBody } }
        }), Encoding.UTF8, "application/json");
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return new(false, null, $"SendGrid returned {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            var id = response.Headers.TryGetValues("X-Message-Id", out var values) ? values.FirstOrDefault() : null;
            return new(true, id ?? $"sendgrid:{Guid.NewGuid():N}");
        }
        catch (Exception exception) { return new(false, null, exception.Message); }
    }
}

public sealed class EmailDeliveryService(AppDbContext db, IEnumerable<IEmailDeliveryProvider> providers, IConfiguration configuration)
{
    public async Task<EmailProviderResult> SendApprovedAsync(Guid tenantId, Guid messageId, CancellationToken ct = default)
    {
        var message = await db.OutreachMessages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == messageId, ct);
        if (message is null) return new(false, null, "Outreach message was not found.");
        if (message.Status is OutreachStatus.Sent or OutreachStatus.Delivered or OutreachStatus.Replied) return new(true, message.ProviderMessageId);
        var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == message.ProspectId, ct);
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == message.CampaignId, ct);
        if (prospect is null || campaign is null) return new(false, null, "Campaign recipient data is incomplete.");
        if (prospect.Email.EndsWith(".example", StringComparison.OrdinalIgnoreCase)) return new(false, null, "Safety block: .example recipients cannot receive real email.");
        if (await IsSuppressedAsync(tenantId, prospect, ct)) return new(false, null, "Recipient is suppressed or has withdrawn marketing consent.");
        var approvalTitle = $"APPROVAL: Send outreach {message.Id}";
        if (!await db.CrmTasks.AnyAsync(x => x.TenantId == tenantId && x.Title == approvalTitle && x.Completed, ct)) return new(false, null, "Human approval is required before sending.");
        var sender = await db.IntegrationConnections.AsNoTracking().FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Provider == "email-sender" && x.Status == IntegrationStatus.Connected, ct);
        if (sender is null) return new(false, null, "No verified sender identity is configured.");
        using var settings = JsonDocument.Parse(sender.SettingsJson);
        var fromEmail = settings.RootElement.GetProperty("email").GetString() ?? "";
        var fromName = settings.RootElement.TryGetProperty("name", out var name) ? name.GetString() ?? campaign.SenderName : campaign.SenderName;
        if (!settings.RootElement.TryGetProperty("verified", out var verified) || !verified.GetBoolean()) return new(false, null, "Sender identity is not verified.");
        var providerName = configuration["Email:Provider"] ?? "disabled";
        var provider = providers.FirstOrDefault(x => x.Name.Equals(providerName, StringComparison.OrdinalIgnoreCase));
        if (provider is null) return new(false, null, $"Email provider '{providerName}' is not enabled.");
        var result = await provider.SendAsync(new EmailEnvelope(fromEmail, fromName, prospect.Email, prospect.ContactName, message.Subject, message.Body.Replace("\n", "<br>"), message.Body), ct);
        if (result.Success)
        {
            message.Status = OutreachStatus.Sent; message.ProviderMessageId = result.ProviderMessageId ?? ""; message.SentAtUtc = DateTime.UtcNow;
            db.UsageRecords.Add(new UsageRecord { TenantId = tenantId, Meter = "emails_sent", Quantity = 1, ReferenceId = message.Id.ToString() });
            await db.SaveChangesAsync(ct);
        }
        return result;
    }

    private async Task<bool> IsSuppressedAsync(Guid tenantId, Prospect prospect, CancellationToken ct)
    {
        var contactId = prospect.ContactId;
        if (!contactId.HasValue)
            contactId = await db.Contacts.Where(x => x.TenantId == tenantId && x.Email == prospect.Email).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        return contactId.HasValue && await db.ConsentRecords.AnyAsync(x => x.TenantId == tenantId && x.ContactId == contactId && x.Type == "marketing" && !x.Granted, ct);
    }
}
