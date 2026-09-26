using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using LeadsAI.Application;

namespace LeadsAI.Infrastructure;

public sealed class OllamaAiProvider(HttpClient http, IConfiguration configuration) : IAiProvider
{
    public async Task<string> CompleteAsync(string system, string user, CancellationToken ct = default)
    {
        var baseUrl = (configuration["Ai:BaseUrl"] ?? "http://localhost:11434").TrimEnd('/') + "/";
        var model = configuration["Ai:Model"] ?? "qwen3:8b";

        http.BaseAddress = new Uri(baseUrl);

        var payload = new
        {
            model,
            system,
            prompt = user,
            stream = false,
            format = "json",
            options = new
            {
                temperature = 0.3
            }
        };

        using var response = await http.PostAsync(
            "api/generate",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(
                $"Local AI provider request failed ({(int)response.StatusCode}). Make sure Ollama is running and model '{model}' is installed.");

        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("response").GetString() ?? string.Empty;
    }
}
