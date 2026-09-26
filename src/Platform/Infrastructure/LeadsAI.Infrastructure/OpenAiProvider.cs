using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using LeadsAI.Application;

namespace LeadsAI.Infrastructure;

public sealed class OpenAiProvider(HttpClient http, IConfiguration configuration, LocalAiProvider fallback) : IAiProvider
{
    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var apiKey = configuration["Ai:ApiKey"] ?? configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return await fallback.CompleteAsync(system, user, ct);

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
            temperature = 0.3
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
