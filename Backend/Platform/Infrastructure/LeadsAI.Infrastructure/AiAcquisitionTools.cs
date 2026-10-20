using System.Text.Json;
using LeadsAI.Application;
using LeadsAI.Infrastructure.Acquisition;

namespace LeadsAI.Infrastructure;

public sealed class SearchProspectsTool(ProspectDiscoveryService discovery) : IAiTool
{
    public string Name => "SearchProspects";

    public async Task<AiToolResult> ExecuteAsync(AiToolContext context, string inputJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(inputJson);
        if (!doc.RootElement.TryGetProperty("icpId", out var p) || !Guid.TryParse(p.GetString(), out var icpId))
            return new(false, "{}", "A valid icpId is required.");

        var options = new DiscoveryRunOptions(
            Source: doc.RootElement.TryGetProperty("source", out var s) ? s.GetString() : "serpapi",
            Region: doc.RootElement.TryGetProperty("region", out var r) ? r.GetString() : null,
            MaximumResults: doc.RootElement.TryGetProperty("maximumResults", out var n) && n.TryGetInt32(out var count) ? count : 25,
            MinimumScore: doc.RootElement.TryGetProperty("minimumScore", out var score) && score.TryGetInt32(out var min) ? min : 70,
            TargetListName: doc.RootElement.TryGetProperty("targetListName", out var name) ? name.GetString() : null,
            CreateTargetList: true,
            TenantId: context.TenantId);

        var result = await discovery.DiscoverAsync(context.TenantId, icpId, options, ct);
        return new(true, JsonSerializer.Serialize(result));
    }
}

public sealed class SearchKnowledgeAiTool(IKnowledgeRetriever retriever) : IAiTool
{
    public string Name => "SearchKnowledge";
    public async Task<AiToolResult> ExecuteAsync(AiToolContext context, string inputJson, CancellationToken ct = default)
    {
        using var doc = JsonDocument.Parse(inputJson);
        var query = doc.RootElement.TryGetProperty("query", out var q) ? q.GetString() ?? string.Empty : string.Empty;
        if (string.IsNullOrWhiteSpace(query)) return new(false, "[]", "query is required.");
        var hits = await retriever.SearchAsync(context.TenantId, query, 8, ct);
        return new(true, JsonSerializer.Serialize(hits));
    }
}
