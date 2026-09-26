using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using LeadsAI.Application;

namespace LeadsAI.Infrastructure;

public sealed class OpenAiProvider(HttpClient http, IConfiguration configuration) : IAiProvider
{
    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var apiKey = configuration["Ai:ApiKey"] ?? configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI provider is not configured. Set Ai:ApiKey or OpenAI:ApiKey.");

        var model = configuration["Ai:Model"] ?? "gpt-5-mini";
        var baseUrl = (configuration["Ai:BaseUrl"] ?? "https://api.openai.com/v1/").TrimEnd('/') + "/";
        http.BaseAddress = new Uri(baseUrl);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = 0.3,
            response_format = new { type = "json_object" }
        };

        using var response = await http.PostAsync(
            "chat/completions",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"AI provider request failed ({(int)response.StatusCode}).");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? string.Empty;
    }
}
