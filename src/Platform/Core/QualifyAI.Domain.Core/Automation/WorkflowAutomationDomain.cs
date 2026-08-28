using System.Text.Json;

namespace QualifyAI.Domain;

public class QualificationFlow : TenantEntity
{
    public string Name { get; set; } = "Default Qualification";
    public bool Active { get; set; } = true;
}

public class WorkflowNode : TenantEntity
{
    public Guid FlowId { get; set; }
    public string NodeKey { get; set; } = string.Empty;
    public string Type { get; set; } = "question";
    public string ConfigJson { get; set; } = "{}";
    public int X { get; set; }
    public int Y { get; set; }

    public static WorkflowNode Create(Guid tenantId, Guid flowId, string nodeKey, string type, string configJson, int x, int y, Guid? id = null)
    {
        if (flowId == Guid.Empty) throw new InvalidOperationException("Workflow flow is required.");
        if (string.IsNullOrWhiteSpace(nodeKey)) throw new InvalidOperationException("Workflow node key is required.");
        if (string.IsNullOrWhiteSpace(type)) throw new InvalidOperationException("Workflow node type is required.");
        EnsureJson(configJson, "Workflow node configuration");
        return new WorkflowNode
        {
            Id = id is { } value && value != Guid.Empty ? value : Guid.NewGuid(),
            TenantId = tenantId,
            FlowId = flowId,
            NodeKey = nodeKey.Trim(),
            Type = type.Trim().ToLowerInvariant(),
            ConfigJson = string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson,
            X = x,
            Y = y
        };
    }

    internal static void EnsureJson(string? json, string label)
    {
        try { using var _ = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json); }
        catch (JsonException ex) { throw new InvalidOperationException($"{label} must be valid JSON.", ex); }
    }
}

public class WorkflowEdge : TenantEntity
{
    public Guid FlowId { get; set; }
    public string FromNodeKey { get; set; } = string.Empty;
    public string ToNodeKey { get; set; } = string.Empty;
    public string ConditionJson { get; set; } = "{}";

    public static WorkflowEdge Create(Guid tenantId, Guid flowId, string fromNodeKey, string toNodeKey, string conditionJson, Guid? id = null)
    {
        if (string.IsNullOrWhiteSpace(fromNodeKey) || string.IsNullOrWhiteSpace(toNodeKey))
            throw new InvalidOperationException("Workflow edge endpoints are required.");
        if (string.Equals(fromNodeKey.Trim(), toNodeKey.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Workflow self-referencing edges are not allowed.");
        WorkflowNode.EnsureJson(conditionJson, "Workflow edge condition");
        return new WorkflowEdge
        {
            Id = id is { } value && value != Guid.Empty ? value : Guid.NewGuid(),
            TenantId = tenantId,
            FlowId = flowId,
            FromNodeKey = fromNodeKey.Trim(),
            ToNodeKey = toNodeKey.Trim(),
            ConditionJson = string.IsNullOrWhiteSpace(conditionJson) ? "{}" : conditionJson
        };
    }
}

public static class WorkflowDesigner
{
    public static (IReadOnlyList<WorkflowNode> Nodes, IReadOnlyList<WorkflowEdge> Edges) Build(
        Guid tenantId, Guid flowId, IEnumerable<WorkflowNode> nodes, IEnumerable<WorkflowEdge> edges)
    {
        var builtNodes = nodes.Select(x => WorkflowNode.Create(tenantId, flowId, x.NodeKey, x.Type, x.ConfigJson, x.X, x.Y)).ToArray();
        if (builtNodes.Length == 0) throw new InvalidOperationException("A workflow requires at least one node.");
        var keys = builtNodes.Select(x => x.NodeKey).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (keys.Count != builtNodes.Length) throw new InvalidOperationException("Workflow node keys must be unique.");
        var builtEdges = edges.Select(x => WorkflowEdge.Create(tenantId, flowId, x.FromNodeKey, x.ToNodeKey, x.ConditionJson)).ToArray();
        if (builtEdges.Any(x => !keys.Contains(x.FromNodeKey) || !keys.Contains(x.ToNodeKey)))
            throw new InvalidOperationException("Workflow edges must reference existing nodes.");
        if (builtEdges.GroupBy(x => new { From = x.FromNodeKey.ToLowerInvariant(), To = x.ToNodeKey.ToLowerInvariant() }).Any(x => x.Count() > 1))
            throw new InvalidOperationException("Duplicate workflow edges are not allowed.");
        return (builtNodes, builtEdges);
    }
}

public class AutomationRule : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public string Trigger { get; set; } = string.Empty;
    public string ConditionsJson { get; set; } = "[]";
    public string ActionsJson { get; set; } = "[]";
    public bool Active { get; set; } = true;

    public static AutomationRule Create(Guid tenantId, string name, string trigger, string conditionsJson, string actionsJson, bool active)
    {
        var rule = new AutomationRule { TenantId = tenantId };
        rule.UpdateConfiguration(name, trigger, conditionsJson, actionsJson, active);
        return rule;
    }

    public void UpdateConfiguration(string name, string trigger, string conditionsJson, string actionsJson, bool active)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new InvalidOperationException("Automation name is required.");
        if (string.IsNullOrWhiteSpace(trigger)) throw new InvalidOperationException("Automation trigger is required.");
        WorkflowNode.EnsureJson(conditionsJson, "Automation conditions");
        WorkflowNode.EnsureJson(actionsJson, "Automation actions");
        using var actions = JsonDocument.Parse(string.IsNullOrWhiteSpace(actionsJson) ? "[]" : actionsJson);
        if (actions.RootElement.ValueKind != JsonValueKind.Array || actions.RootElement.GetArrayLength() == 0)
            throw new InvalidOperationException("Automation requires at least one action.");
        Name = name.Trim();
        Trigger = trigger.Trim().ToLowerInvariant();
        ConditionsJson = string.IsNullOrWhiteSpace(conditionsJson) ? "[]" : conditionsJson;
        ActionsJson = actionsJson;
        Active = active;
        Touch();
    }
}

public class AutomationRun : TenantEntity
{
    public Guid RuleId { get; set; }
    public string TriggerDataJson { get; set; } = "{}";
    public string Status { get; set; } = "pending";
    public string LogJson { get; set; } = "[]";
    public DateTime? CompletedAtUtc { get; set; }

    public static AutomationRun Create(Guid tenantId, Guid ruleId, string triggerDataJson)
    {
        if (ruleId == Guid.Empty) throw new InvalidOperationException("Automation rule is required.");
        WorkflowNode.EnsureJson(triggerDataJson, "Automation trigger data");
        return new AutomationRun { TenantId = tenantId, RuleId = ruleId, TriggerDataJson = triggerDataJson, Status = "pending" };
    }

    public void Start() { EnsureStatus("pending"); Status = "running"; Touch(); }
    public void Complete(string logJson) { EnsureStatus("running"); WorkflowNode.EnsureJson(logJson, "Automation log"); Status = "completed"; LogJson = logJson; CompletedAtUtc = DateTime.UtcNow; Touch(); }
    public void Fail(string logJson) { EnsureStatus("running"); WorkflowNode.EnsureJson(logJson, "Automation log"); Status = "failed"; LogJson = logJson; CompletedAtUtc = DateTime.UtcNow; Touch(); }

    private void EnsureStatus(string expected)
    {
        if (!string.Equals(Status, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Automation run must be {expected}.");
    }
}
