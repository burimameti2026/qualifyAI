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

        if (existing.Count > 0)
            return existing;

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

            if (!document.RootElement.TryGetProperty("stages", out var stages) ||
                stages.ValueKind != JsonValueKind.Array)
                return [];

            return stages.EnumerateArray()
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
