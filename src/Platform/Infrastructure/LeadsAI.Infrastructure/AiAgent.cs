using System.Text.Json;
using LeadsAI.Application;

namespace LeadsAI.Infrastructure;

public sealed class AiAgent(IAiProvider provider, IAiToolRegistry tools) : IAiAgent
{
    private static readonly HashSet<string> ApprovalRequiredTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "CreateLead", "CreateTicket", "CreateIcp", "CreateCampaign", "CreateTargetList", "RunAutonomousAcquisition"
    };

    public async Task<AiAgentResult> RunAsync(AiAgentRequest request, AiToolContext context, CancellationToken ct = default)
    {
        var available = string.Join(", ", tools.Names);
        var system = "You are the execution brain of LeadsAI. Understand the business goal and tenant context. " +
            "Available tools: " + available + ". Never invent data. If information is missing, ask for it. " +
            "If a tool is useful, return ONLY JSON with message,suggestions,nextAction,tool,toolInput. Use exactly one tool. " +
            "Read/search tools may execute immediately. Write or external-action tools must NEVER execute immediately: " +
            "return the proposed tool and exact toolInput for explicit approval. Otherwise tool must be null.";
        var raw = await provider.CompleteAsync(system, $"TenantId: {context.TenantId}\nContext: {request.ContextJson}\nGoal: {request.Goal}", ct);
        try
        {
            var json = raw.Trim();
            if (json.StartsWith("```", StringComparison.Ordinal)) { var first = json.IndexOf('\n'); var last = json.LastIndexOf("```", StringComparison.Ordinal); if (first >= 0 && last > first) json = json[(first + 1)..last].Trim(); }
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var message = root.TryGetProperty("message", out var m) ? m.GetString() ?? raw : raw;
            var suggestions = root.TryGetProperty("suggestions", out var s) && s.ValueKind == JsonValueKind.Array ? s.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)).Take(3).Cast<string>().ToArray() : Array.Empty<string>();
            var next = root.TryGetProperty("nextAction", out var n) ? n.GetString() : null;
            var toolName = root.TryGetProperty("tool", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() : null;
            if (string.IsNullOrWhiteSpace(toolName)) return new(message, suggestions, next, null, null);
            var tool = tools.Resolve(toolName);
            if (tool is null) return new(message, suggestions, $"Tool '{toolName}' is not available.", null, null);
            var input = root.TryGetProperty("toolInput", out var ti) ? ti.GetRawText() : "{}";
            var requiresApproval = ApprovalRequiredTools.Contains(tool.Name);
            var action = new AiToolAction(tool.Name, input, requiresApproval, requiresApproval ? "write" : "read");
            if (requiresApproval) return new(message, suggestions, next, tool.Name, null, input, true, action);
            var result = await tool.ExecuteAsync(context, input, ct);
            return new(message, suggestions, next, tool.Name, result.Json, input, false, action);
        }
        catch { return new(raw, Array.Empty<string>(), "Review the recommendation and continue.", null, null); }
    }
}
