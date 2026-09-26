using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Ai)]
[RequirePermission(QualifyAiPermissions.AgentsRead)]
[Route("api/ai/advisor/ask")]
public sealed class AiAdvisorModelController(IHttpClientFactory httpClientFactory, IConfiguration configuration) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AdvisorAskRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Message)) return BadRequest(new { detail = "Tell the advisor what you want to achieve." });
        var context = JsonSerializer.Serialize(input.Context ?? new Dictionary<string, object?>());
        if (context.Length > 16000) context = context[..16000];
        var apiKey = configuration["Ai:ApiKey"] ?? configuration["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey)) return Ok(LocalRecommendation(input.Message, context));

        var model = configuration["Ai:Model"] ?? "gpt-5-mini";
        var baseUrl = (configuration["Ai:BaseUrl"] ?? "https://api.openai.com/v1/").TrimEnd('/') + "/";
        var prompt = """You are the persistent AI Advisor inside LeadsAI. You are a product copilot, not a generic chatbot.
Adapt to the user's current context. The user may be configuring logistics, but they may also be unsure what industry or pack to choose.
First understand the goal. If the user is unsure about a pack, inspect availablePacks in context. Compare the user's business, buyer, offer and desired outcome against each pack's description/name. Recommend an existing pack only when the match is clear; otherwise explicitly say that a new pack is more appropriate. If the business is still unknown, do not guess: ask up to three focused questions (what they sell, who buys it, desired outcome).
Help with ICP, offers, discovery keywords, qualification, enrichment, target lists, campaigns and outreach.
Give concrete text the user can paste. When recommending a pack, include pack name, why it fits, what to change, and the next step. Prefer one strong recommendation plus up to two alternatives.
Never invent facts about the user's business. Say when information is missing and ask for the minimum useful detail.
Do not claim to have changed, saved, sent, or executed anything.
Return ONLY JSON: {"message":"...","suggestions":["..."],"nextAction":"...","field":"optional field name","action":"optional action"}.""";
        var payload = new { model, messages = new[] { new { role = "system", content = prompt }, new { role = "user", content = $"Context:\n{context}\n\nUser:\n{input.Message.Trim()}" } }, temperature = 0.3 };
        var client = httpClientFactory.CreateClient();
        client.BaseAddress = new Uri(baseUrl);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        using var response = await client.PostAsync("chat/completions", new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"), ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode) return StatusCode((int)response.StatusCode, new { detail = "AI model request failed.", provider = body.Length > 500 ? body[..500] : body });
        using var doc = JsonDocument.Parse(body);
        var content = doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
        return Ok(Parse(content));
    }

    private static object LocalRecommendation(string message, string context)
    {
        var lower = message.ToLowerInvariant();
        if (lower.Contains("pack") && (lower.Contains("not sure") || lower.Contains("which") || lower.Contains("what")))
            return new { message = "I can recommend a pack once I know the business you want to target. Tell me what the company sells, who buys it, and the main outcome you want from the campaign. If an existing pack matches, I will point you to it; otherwise we can create a new one.", suggestions = new[] { "Help me choose a pack", "Create a new pack", "I am targeting logistics companies" }, nextAction = "Describe the business, buyer and desired outcome.", field = "" };
        return new { message = "I can guide you through this configuration. Connect an AI model with Ai:ApiKey to get model-generated recommendations and content.", suggestions = new[] { "What should I do next?", "What should I write?" }, nextAction = "Give me your goal and I will suggest the next step.", field = "" };
    }

    private static object Parse(string raw)
    {
        try { var json = raw.Trim(); if (json.StartsWith("```")) { var first = json.IndexOf('\n'); var last = json.LastIndexOf("```"); if (first >= 0 && last > first) json = json[(first + 1)..last].Trim(); } using var doc = JsonDocument.Parse(json); var root=doc.RootElement; return new { message=root.GetProperty("message").GetString() ?? raw, suggestions=root.TryGetProperty("suggestions",out var s)&&s.ValueKind==JsonValueKind.Array?s.EnumerateArray().Select(x=>x.GetString()).Where(x=>!string.IsNullOrWhiteSpace(x)).Take(3).Cast<string>().ToArray():Array.Empty<string>(), nextAction=root.TryGetProperty("nextAction",out var n)?n.GetString():null, field=root.TryGetProperty("field",out var f)?f.GetString():null }; } catch { return new { message=raw, suggestions=Array.Empty<string>(), nextAction=(string?)null, field=(string?)null }; }
}

public sealed record AdvisorAskRequest(string Message, Dictionary<string, object?>? Context);