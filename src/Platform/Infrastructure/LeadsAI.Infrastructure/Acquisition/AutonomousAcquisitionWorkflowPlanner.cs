using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

public interface IAutonomousAcquisitionWorkflowPlanner
{
    Task<IReadOnlyList<AutonomousAcquisitionTask>> EnsurePlanAsync(
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        CancellationToken ct = default,
        string? campaignPlanJson = null);
}

public sealed class AutonomousAcquisitionWorkflowPlanner(AppDbContext db) : IAutonomousAcquisitionWorkflowPlanner
{
    public async Task<IReadOnlyList<AutonomousAcquisitionTask>> EnsurePlanAsync(
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        CancellationToken ct = default,
        string? campaignPlanJson = null)
    {
        var existing = await db.AutonomousAcquisitionTasks
            .Where(x => x.TenantId == agent.TenantId && x.AgentId == agent.Id && x.RunId == null)
            .OrderBy(x => x.Sequence)
            .ToListAsync(ct);

        // Campaign.PlanJson is the source of truth. Rebuild the reusable plan
        // whenever a campaign definition is supplied so Designer edits cannot
        // leave stale template-generated tasks behind.
        if (string.IsNullOrWhiteSpace(campaignPlanJson) && existing.Count > 0)
            return existing;

        if (!string.IsNullOrWhiteSpace(campaignPlanJson) && existing.Count > 0)
        {
            db.AutonomousAcquisitionTasks.RemoveRange(existing);
            await db.SaveChangesAsync(ct);
        }

        var countries = ReadCountries(agent.CountriesJson);
        var common = new
        {
            agent.TemplateCode,
            agent.Industry,
            agent.Region,
            Countries = countries,
            agent.MinimumScore,
            agent.DailyDiscoveryLimit,
            Keywords = template.Keywords,
            Signals = template.Signals,
            ProspectType = template.ProspectType,
            TargetDefinition = template.TargetDefinition,
            OutreachTemplates = template.OutreachTemplates.Select(x => new { x.Step, x.Name, x.Subject, x.Body, x.DelayHours, x.RequiresApproval })
        };

        var stages = ReadStages(campaignPlanJson);
        var now = DateTime.UtcNow;

        var tasks = stages.Count > 0
            ? BuildTasksFromStages(agent, template, common, stages, now)
            : BuildDefaultTasks(agent, template, common, now);

        db.AutonomousAcquisitionTasks.AddRange(tasks);
        await db.SaveChangesAsync(ct);
        return tasks;
    }

    private static AutonomousAcquisitionTask[] BuildDefaultTasks(
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        object common,
        DateTime now) =>
    [
        New(agent, 1, AutonomousAgentTaskTypes.Discover, "Discover companies",
            "Find companies matching the campaign ICP and discovery strategy.",
            common, AutonomousAgentTaskTypes.Qualify, false, now),
        New(agent, 2, AutonomousAgentTaskTypes.Qualify, "Qualify prospects",
            "Evaluate discovered companies against the campaign qualification rules.",
            new { minimumScore = agent.MinimumScore, signals = template.Signals },
            AutonomousAgentTaskTypes.Enrich, false, now),
        New(agent, 3, AutonomousAgentTaskTypes.Enrich, "Enrich company intelligence",
            "Structure public evidence needed to understand qualified companies and likely buyers.",
            new { fields = new[] { "company", "size", "website", "buyer", "signals" } },
            AutonomousAgentTaskTypes.BuildTargetList, false, now),
        New(agent, 4, AutonomousAgentTaskTypes.BuildTargetList, "Build target list",
            "Put qualified prospects into the campaign target list without duplicates.",
            new { minimumScore = agent.MinimumScore },
            AutonomousAgentTaskTypes.Outreach, false, now),
        New(agent, 5, AutonomousAgentTaskTypes.Outreach, "Prepare outreach",
            "Prepare personalized outreach and stop at the human approval gate.",
            new { approvalRequired = template.OutreachTemplates.Any(x => x.RequiresApproval), dailyLimit = agent.DailyEmailLimit },
            "campaign-approval",
            template.OutreachTemplates.Any(x => x.RequiresApproval), now)
    ];

    private static AutonomousAcquisitionTask[] BuildTasksFromStages(
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        object common,
        IReadOnlyList<CampaignPlanStage> stages,
        DateTime now)
    {
        var executable = stages
            .Select(x => new { Stage = x, Type = MapType(x.Type) })
            .Where(x => x.Type is not null)
            .ToList();

        if (executable.Count == 0)
            return BuildDefaultTasks(agent, template, common, now);

        var approvalStage = stages.FirstOrDefault(x =>
            string.Equals(x.Type, "Approval", StringComparison.OrdinalIgnoreCase));

        var outreachRequiresApproval =
            approvalStage?.RequiresApproval == true ||
            template.OutreachTemplates.Any(x => x.RequiresApproval);

        return executable.Select((item, index) =>
        {
            var next = index + 1 < executable.Count
                ? executable[index + 1].Type!
                : "campaign-approval";

            var requiresApproval =
                item.Stage.RequiresApproval ||
                (item.Type == AutonomousAgentTaskTypes.Outreach && outreachRequiresApproval);

            var configuration = new
            {
                stageId = item.Stage.Id,
                stageType = item.Stage.Type,
                stageConfig = item.Stage.Config,
                campaignStageOrder = item.Stage.Order
            };

            return New(
                agent,
                index + 1,
                item.Type!,
                string.IsNullOrWhiteSpace(item.Stage.Name) ? item.Stage.Type : item.Stage.Name,
                item.Stage.Description ?? $"Campaign stage: {item.Stage.Type}.",
                configuration,
                next,
                requiresApproval,
                now);
        }).ToArray();
    }

    private static string? MapType(string type) =>
        type.Trim().ToLowerInvariant() switch
        {
            "discovery" or "discover" => AutonomousAgentTaskTypes.Discover,
            "qualification" or "qualify" => AutonomousAgentTaskTypes.Qualify,
            "enrichment" or "enrich" => AutonomousAgentTaskTypes.Enrich,
            "targetlist" or "target-list" or "target_list" => AutonomousAgentTaskTypes.BuildTargetList,
            "outreach" => AutonomousAgentTaskTypes.Outreach,
            // Approval and Delivery are lifecycle gates handled by the runtime,
            // not autonomous worker task executors.
            "approval" or "delivery" => null,
            _ => null
        };

    private sealed record CampaignPlanStage(
        string Id,
        string Type,
        string Name,
        string? Description,
        JsonElement Config,
        bool RequiresApproval,
        int Order);

    private static List<CampaignPlanStage> ReadStages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var document = JsonDocument.Parse(json);

            // Graph nodes are the primary designer representation. Keep stages as a
            // backward-compatible execution projection for the current runtime.
            var source = document.RootElement.TryGetProperty("nodes", out var nodes) &&
                         nodes.ValueKind == JsonValueKind.Array
                ? nodes
                : document.RootElement.TryGetProperty("stages", out var stages) &&
                  stages.ValueKind == JsonValueKind.Array
                    ? stages
                    : default;

            if (source.ValueKind != JsonValueKind.Array)
                return [];

            var result = source.EnumerateArray()
                .Select((stage, index) => new CampaignPlanStage(
                    ReadString(stage, "id"),
                    ReadString(stage, "type"),
                    ReadString(stage, "name"),
                    ReadNullableString(stage, "description"),
                    stage.TryGetProperty("config", out var config) && config.ValueKind == JsonValueKind.Object
                        ? config.Clone()
                        : JsonDocument.Parse("{}").RootElement.Clone(),
                    ReadBool(stage, "requiresApproval"),
                    index + 1))
                .Where(x => !string.IsNullOrWhiteSpace(x.Type))
                .ToList();

            // If edges exist, topologically order the executable projection so the
            // runtime follows the same connections the user sees in the canvas.
            if (document.RootElement.TryGetProperty("edges", out var edges) &&
                edges.ValueKind == JsonValueKind.Array &&
                result.Count > 1)
            {
                var byId = result.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
                var next = edges.EnumerateArray()
                    .Where(x => x.TryGetProperty("from", out _) && x.TryGetProperty("to", out _))
                    .Select(x => (From: ReadString(x, "from"), To: ReadString(x, "to")))
                    .Where(x => byId.ContainsKey(x.From) && byId.ContainsKey(x.To))
                    .GroupBy(x => x.From, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(x => x.Key, x => x.First().To, StringComparer.OrdinalIgnoreCase);

                var ordered = new List<CampaignPlanStage>();
                var current = result.FirstOrDefault(x => !result.Any(y => next.TryGetValue(y.Id, out var target) &&
                                                                            string.Equals(target, x.Id, StringComparison.OrdinalIgnoreCase)));
                var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                while (current is not null && visited.Add(current.Id))
                {
                    ordered.Add(current);
                    current = next.TryGetValue(current.Id, out var targetId) && byId.TryGetValue(targetId, out var target)
                        ? target
                        : null;
                }

                if (ordered.Count == result.Count)
                    result = ordered.Select((x, index) => x with { Order = index + 1 }).ToList();
            }

            return result;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string ReadString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? ReadNullableString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool ReadBool(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) &&
        value.ValueKind == JsonValueKind.True &&
        value.GetBoolean();

    private static AutonomousAcquisitionTask New(
        AutonomousAcquisitionAgent agent,
        int sequence,
        string type,
        string name,
        string purpose,
        object configuration,
        string nextStep,
        bool requiresApproval,
        DateTime now) =>
        new()
        {
            TenantId = agent.TenantId,
            AgentId = agent.Id,
            Sequence = sequence,
            Type = type,
            Name = name,
            RequiresApproval = requiresApproval,
            ConfigurationJson = JsonSerializer.Serialize(new { purpose, input = configuration, nextStep }),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static List<string> ReadCountries(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
