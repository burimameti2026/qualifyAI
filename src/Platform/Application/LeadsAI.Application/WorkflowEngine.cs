using System.Globalization;
using System.Text.Json;
using LeadsAI.Domain;

namespace LeadsAI.Application;

public record WorkflowExecutionContext(
    Guid TenantId,
    Guid? LeadId,
    Guid? ContactId,
    Dictionary<string, string> Facts);

public record WorkflowExecutionResult(
    string? NextNodeKey,
    int ScoreDelta,
    List<string> Actions);

/// <summary>
/// Deterministic workflow step evaluator. It selects the first matching outgoing
/// edge, supports simple fact-based branching, and normalizes node types so all
/// industry templates can use the same execution contract.
/// </summary>
public sealed class WorkflowEngine
{
    private static readonly HashSet<string> ActionNodeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "assign", "email", "webhook", "createlead", "createticket", "bookmeeting",
        "discoverprospects", "deduplicate", "enrichcompany", "enrichcontact",
        "qualify", "score", "personalize", "addtotargetlist", "addtocampaign",
        "sendoutreach", "processreply", "createopportunity", "synccrm", "wait", "delay",
        "notify", "createtask", "requestapproval"
    };

    public WorkflowExecutionResult Execute(
        WorkflowNode node,
        IEnumerable<WorkflowEdge> edges,
        WorkflowExecutionContext ctx)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(edges);
        ArgumentNullException.ThrowIfNull(ctx);

        var actions = new List<string>();
        var delta = 0;
        var nodeType = Normalize(node.Type);
        var config = ParseObject(node.ConfigJson, "Workflow node configuration");

        if (nodeType == "score")
        {
            if (config.TryGetProperty("points", out var points) && points.TryGetInt32(out var parsedPoints))
                delta = parsedPoints;
            else if (config.TryGetProperty("delta", out var deltaValue) && deltaValue.TryGetInt32(out var parsedDelta))
                delta = parsedDelta;
        }

        if (ActionNodeTypes.Contains(nodeType))
            actions.Add(nodeType);

        var next = edges
            .Where(e => string.Equals(e.FromNodeKey, node.NodeKey, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault(e => ConditionMatches(e.ConditionJson, ctx.Facts))
            ?.ToNodeKey;

        return new(next, delta, actions);
    }

    private static bool ConditionMatches(string? conditionJson, IReadOnlyDictionary<string, string> facts)
    {
        if (string.IsNullOrWhiteSpace(conditionJson))
            return true;

        using var document = ParseObject(conditionJson, "Workflow edge condition");
        if (document.ValueKind == JsonValueKind.Undefined || document.EnumerateObject().Count() == 0)
            return true;

        var field = ReadString(document, "fact")
            ?? ReadString(document, "field")
            ?? ReadString(document, "key");
        if (string.IsNullOrWhiteSpace(field))
            return true;

        if (!facts.TryGetValue(field, out var actual))
            return false;

        var expected = ReadString(document, "value") ?? string.Empty;
        var op = (ReadString(document, "operator") ?? ReadString(document, "op") ?? "eq").Trim().ToLowerInvariant();

        return op switch
        {
            "eq" or "=" or "==" => string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "neq" or "!=" => !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase),
            "contains" => actual.Contains(expected, StringComparison.OrdinalIgnoreCase),
            "startswith" => actual.StartsWith(expected, StringComparison.OrdinalIgnoreCase),
            "endswith" => actual.EndsWith(expected, StringComparison.OrdinalIgnoreCase),
            "gt" or ">" => Compare(actual, expected) > 0,
            "gte" or ">=" => Compare(actual, expected) >= 0,
            "lt" or "<" => Compare(actual, expected) < 0,
            "lte" or "<=" => Compare(actual, expected) <= 0,
            "in" => expected.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => string.Equals(actual, x, StringComparison.OrdinalIgnoreCase)),
            "notin" => !expected.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(x => string.Equals(actual, x, StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
    }

    private static int Compare(string actual, string expected)
    {
        if (decimal.TryParse(actual, NumberStyles.Any, CultureInfo.InvariantCulture, out var actualNumber) &&
            decimal.TryParse(expected, NumberStyles.Any, CultureInfo.InvariantCulture, out var expectedNumber))
            return actualNumber.CompareTo(expectedNumber);

        return string.Compare(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonElement ParseObject(string? json, string label)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"{label} must be a JSON object.");
            return document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"{label} must be valid JSON.", ex);
        }
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace("_", string.Empty).Replace("-", string.Empty);

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : element.TryGetProperty(name, out value) && value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False
                ? value.ToString()
                : null;
}
