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
        CancellationToken ct = default);
}

public sealed class AutonomousAcquisitionWorkflowPlanner(AppDbContext db) : IAutonomousAcquisitionWorkflowPlanner
{
    public async Task<IReadOnlyList<AutonomousAcquisitionTask>> EnsurePlanAsync(
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        CancellationToken ct = default)
    {
        var existing = await db.AutonomousAcquisitionTasks
            .Where(x => x.TenantId == agent.TenantId && x.AgentId == agent.Id)
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

        var now = DateTime.UtcNow;
        var tasks = new[]
        {
            New(agent, 1, AutonomousAgentTaskTypes.Discover, "Discover companies", false, common, now),
            New(agent, 2, AutonomousAgentTaskTypes.Qualify, "Qualify prospects", false, new { minimumScore = agent.MinimumScore, signals = template.Signals }, now),
            New(agent, 3, AutonomousAgentTaskTypes.Enrich, "Enrich company intelligence", false, new { fields = new[] { "company", "size", "website", "buyer", "signals" } }, now),
            New(agent, 4, AutonomousAgentTaskTypes.BuildTargetList, "Build target list", false, new { minimumScore = agent.MinimumScore }, now),
            New(agent, 5, AutonomousAgentTaskTypes.Outreach, "Prepare outreach", true, new { approvalRequired = true, dailyLimit = agent.DailyEmailLimit }, now)
        };

        db.AutonomousAcquisitionTasks.AddRange(tasks);
        await db.SaveChangesAsync(ct);
        return tasks;
    }

    private static AutonomousAcquisitionTask New(
        AutonomousAcquisitionAgent agent,
        int sequence,
        string type,
        string name,
        bool requiresApproval,
        object configuration,
        DateTime now) =>
        new()
        {
            TenantId = agent.TenantId,
            AgentId = agent.Id,
            Sequence = sequence,
            Type = type,
            Name = name,
            RequiresApproval = requiresApproval,
            ConfigurationJson = JsonSerializer.Serialize(configuration),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    private static List<string> ReadCountries(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }
}
