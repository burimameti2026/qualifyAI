using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace QualifyAI.Infrastructure.Email;

public sealed record BrevoSenderResult(
    bool Success,
    bool Verified,
    long? SenderId = null,
    string? Error = null);

public sealed class BrevoEmailProvider(HttpClient http, IConfiguration configuration) : IEmailDeliveryProvider
{
    public string Name => "brevo";

    public async Task<EmailProviderResult> SendAsync(EmailEnvelope message, CancellationToken ct = default)
    {
        var apiKey = ApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(false, null, "Email:Brevo:ApiKey is not configured.");

        using var request = CreateRequest(HttpMethod.Post, "smtp/email", apiKey);
        request.Content = JsonContent.Create(new
        {
            sender = new { email = message.FromEmail, name = message.FromName },
            to = new[] { new { email = message.ToEmail, name = message.ToName } },
            subject = message.Subject,
            htmlContent = message.HtmlBody,
            textContent = message.TextBody,
            tags = new[] { "qualifyai-outreach" }
        });

        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return new(false, null, $"Brevo returned {(int)response.StatusCode}: {ReadError(body)}");

            using var json = JsonDocument.Parse(body);
            var messageId = json.RootElement.TryGetProperty("messageId", out var id)
                ? id.GetString()
                : null;
            return new(true, messageId ?? $"brevo:{Guid.NewGuid():N}");
        }
        catch (Exception exception)
        {
            return new(false, null, exception.Message);
        }
    }

    public async Task<BrevoSenderResult> EnsureSenderAsync(
        string email,
        string name,
        CancellationToken ct = default)
    {
        var existing = await FindSenderAsync(email, ct);
        if (!existing.Success || existing.SenderId.HasValue)
            return existing;

        var apiKey = ApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(false, false, Error: "Email:Brevo:ApiKey is not configured.");

        using var request = CreateRequest(HttpMethod.Post, "senders", apiKey);
        request.Content = JsonContent.Create(new { email, name });

        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return new(false, false, Error: $"Brevo returned {(int)response.StatusCode}: {ReadError(body)}");

            using var json = JsonDocument.Parse(body);
            var senderId = json.RootElement.TryGetProperty("id", out var id) && id.TryGetInt64(out var value)
                ? value
                : (long?)null;
            return new(true, false, senderId);
        }
        catch (Exception exception)
        {
            return new(false, false, Error: exception.Message);
        }
    }

    public async Task<BrevoSenderResult> FindSenderAsync(string email, CancellationToken ct = default)
    {
        var apiKey = ApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            return new(false, false, Error: "Email:Brevo:ApiKey is not configured.");

        using var request = CreateRequest(HttpMethod.Get, "senders", apiKey);
        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                return new(false, false, Error: $"Brevo returned {(int)response.StatusCode}: {ReadError(body)}");

            using var json = JsonDocument.Parse(body);
            if (!json.RootElement.TryGetProperty("senders", out var senders))
                return new(true, false);

            foreach (var sender in senders.EnumerateArray())
            {
                var senderEmail = sender.TryGetProperty("email", out var value) ? value.GetString() : null;
                if (!string.Equals(senderEmail, email, StringComparison.OrdinalIgnoreCase))
                    continue;

                var active = sender.TryGetProperty("active", out var activeValue) && activeValue.GetBoolean();
                var id = sender.TryGetProperty("id", out var idValue) && idValue.TryGetInt64(out var senderId)
                    ? senderId
                    : (long?)null;
                return new(true, active, id);
            }

            return new(true, false);
        }
        catch (Exception exception)
        {
            return new(false, false, Error: exception.Message);
        }
    }

    private string? ApiKey() => configuration["Email:Brevo:ApiKey"];

    private static HttpRequestMessage CreateRequest(HttpMethod method, string path, string apiKey)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("api-key", apiKey);
        request.Headers.Accept.ParseAdd("application/json");
        return request;
    }

    private static string ReadError(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return "Empty response.";

        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("message", out var message)
                ? message.GetString() ?? body
                : body;
        }
        catch (JsonException)
        {
            return body;
        }
    }
}
