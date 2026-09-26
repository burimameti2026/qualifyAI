using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using LeadsAI.Application;

namespace LeadsAI.Infrastructure;

public sealed class OpenRouterAiProvider(HttpClient http, IConfiguration configuration) : IAiProvider
{
    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var apiKey = configuration["OpenRouter:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Online AI is not configured. Set OpenRouter:ApiKey via OPENROUTER_API_KEY in Docker Compose environment.");

        var baseUrl = (configuration["OpenRouter:BaseUrl"] ?? "https://openrouter.ai/api/v1/").TrimEnd('/') + "/";
        var model = configuration["OpenRouter:Model"] ?? "qwen/qwen3.8-27b:free";

        http.BaseAddress = new Uri(baseUrl);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        http.DefaultRequestHeaders.Remove("HTTP-Referer");
        http.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", configuration["OpenRouter:Referer"] ?? "https://leadsai.app");
        http.DefaultRequestHeaders.Remove("X-Title");
        http.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", configuration["OpenRouter:Title"] ?? "LeadsAI");

        var payload = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            },
            temperature = 0.2,
            max_tokens = 800,
            response_format = new { type = "json_object" }
        };

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

        try
        {
            using var response = await http.PostAsync(
                "chat/completions",
                new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
                timeoutCts.Token);

            var body = await response.Content.ReadAsStringAsync(timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
        {
                var detail = TryGetError(body);
                throw new InvalidOperationException(
                    $"Online AI provider request failed ({(int)response.StatusCode}). {detail}");
            }

            using var doc = JsonDocument.Parse(body);
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "Online AI provider timed out after 60 seconds. The free OpenRouter model is currently slow or unavailable. Please retry.");
        }
    }

    private static string TryGetError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message))
                return message.GetString() ?? "The provider returned an error.";
        }
        catch
        {
        }

        return "The provider did not return a usable response.";
    }
}
