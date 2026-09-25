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

        var now = DateTime.UtcNow;
        var tasks = new[]
        {
            New(agent, 1, AutonomousAgentTaskTypes.Discover, "Discover companies", "Find companies matching the installed package ICP and search strategy.", common, AutonomousAgentTaskTypes.Qualify, false, now),
            New(agent, 2, AutonomousAgentTaskTypes.Qualify, "Qualify prospects", "Evaluate discovered companies against the package qualification rules and score threshold.", new { minimumScore = agent.MinimumScore, signals = template.Signals }, AutonomousAgentTaskTypes.Enrich, false, now),
            New(agent, 3, AutonomousAgentTaskTypes.Enrich, "Enrich company intelligence", "Preserve and structure public evidence needed to understand the qualified company and likely buyer.", new { fields = new[] { "company", "size", "website", "buyer", "signals" } }, AutonomousAgentTaskTypes.BuildTargetList, false, now),
            New(agent, 4, AutonomousAgentTaskTypes.BuildTargetList, "Build target list", "Put qualified prospects into the campaign container target list without duplicates.", new { minimumScore = agent.MinimumScore }, AutonomousAgentTaskTypes.Outreach, false, now),
            New(agent, 5, AutonomousAgentTaskTypes.Outreach, "Prepare outreach", "Create campaign outreach messages from the installed package templates and stop at human approval when required.", new { approvalRequired = template.OutreachTemplates.Any(x => x.RequiresApproval), dailyLimit = agent.DailyEmailLimit }, "campaign-approval", template.OutreachTemplates.Any(x => x.RequiresApproval), now)
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
