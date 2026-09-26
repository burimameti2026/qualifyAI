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
public sealed class AiAdvisorModelController(IAiProvider provider) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Ask([FromBody] AdvisorAskRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Message)) return BadRequest(new { detail = "Tell the advisor what you want to achieve." });
        var context = JsonSerializer.Serialize(input.Context ?? new Dictionary<string, object?>());
        if (context.Length > 16000) context = context[..16000];
        var user = $"Context:\n{context}\n\nUser:\n{input.Message.Trim()}";
        try
        {
            var raw = await provider.CompleteAsync(prompt, user, ct);
            return Ok(Parse(raw));
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status502BadGateway, new { detail = "AI advisor provider failed.", error = ex.Message });
        }

    }

    private static object Parse(string raw)
    {
        try { var json = raw.Trim(); if (json.StartsWith("```")) { var first = json.IndexOf('\n'); var last = json.LastIndexOf("```"); if (first >= 0 && last > first) json = json[(first + 1)..last].Trim(); } using var doc = JsonDocument.Parse(json); var root=doc.RootElement; return new { message=root.GetProperty("message").GetString() ?? raw, suggestions=root.TryGetProperty("suggestions",out var s)&&s.ValueKind==JsonValueKind.Array?s.EnumerateArray().Select(x=>x.GetString()).Where(x=>!string.IsNullOrWhiteSpace(x)).Take(3).Cast<string>().ToArray():Array.Empty<string>(), nextAction=root.TryGetProperty("nextAction",out var n)?n.GetString():null, field=root.TryGetProperty("field",out var f)?f.GetString():null }; } catch { return new { message=raw, suggestions=Array.Empty<string>(), nextAction=(string?)null, field=(string?)null }; }
} }

public sealed record AdvisorAskRequest(string Message, Dictionary<string, object?>? Context);