using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Ai)]
[RequirePermission(QualifyAiPermissions.AgentsRead)]
[Route("api/ai/advisor")]
public sealed class AiAdvisorController(IAiProvider ai, ITenantContext tenant) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Advise([FromBody] AiAdvisorRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Message))
            return BadRequest(new { detail = "Tell the AI Advisor what you are trying to do." });

        var contextJson = JsonSerializer.Serialize(input.Context ?? new Dictionary<string, object?>());
        if (contextJson.Length > 12000) contextJson = contextJson[..12000];

        var system = """
You are the LeadsAI Workspace AI Advisor.
Your job is to guide a user through the product, not just answer questions.
Be practical, concise, and specific.

You must:
- explain what the current section/field does in simple language;
- tell the user what they should do next;
- suggest exactly what to write when the user is editing text;
- identify obvious gaps or weak configuration from the supplied context;
- give a concrete example when useful;
- never claim that you changed or sent anything unless an action was actually executed;
- do not invent database values that are not present in context;
- prefer step-by-step guidance over generic explanations.

Return ONLY valid JSON:
{
  "message": "short useful answer",
  "suggestions": ["concrete suggestion 1", "concrete suggestion 2"],
  "nextAction": "one concise next step"
}

Keep message under 1200 characters and suggestions to at most 3 items.
""";

        var user = $"Tenant: {tenant.TenantId()}\nCurrent workspace context:\n{contextJson}\n\nUser request:\n{input.Message.Trim()}";
        var raw = await ai.CompleteAsync(system, user, ct);
        return Ok(ParseResponse(raw));
    }

    private static AiAdvisorResponse ParseResponse(string raw)
    {
        try
        {
            var json = raw.Trim();
            if (json.StartsWith("```"))
            {
                var firstNewLine = json.IndexOf('\n');
                var lastFence = json.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewLine >= 0 && lastFence > firstNewLine) json = json[(firstNewLine + 1)..lastFence].Trim();
            }
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var message = root.TryGetProperty("message", out var messageElement) ? messageElement.GetString() : null;
            var suggestions = root.TryGetProperty("suggestions", out var suggestionsElement) && suggestionsElement.ValueKind == JsonValueKind.Array
                ? suggestionsElement.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(3).Cast<string>().ToArray()
                : Array.Empty<string>();
            var nextAction = root.TryGetProperty("nextAction", out var nextElement) ? nextElement.GetString() : null;
            return new AiAdvisorResponse(string.IsNullOrWhiteSpace(message) ? raw.Trim() : message.Trim(), suggestions, string.IsNullOrWhiteSpace(nextAction) ? null : nextAction.Trim());
        }
        catch (JsonException)
        {
            return new AiAdvisorResponse(raw.Trim(), Array.Empty<string>(), null);
        }
    }
}

public sealed record AiAdvisorRequest(string Message, Dictionary<string, object?>? Context);
public sealed record AiAdvisorResponse(string Message, IReadOnlyList<string> Suggestions, string? NextAction);