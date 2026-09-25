using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Application;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

public interface IAutonomousAcquisitionRunOrchestrator
{
    Task ExecuteAsync(Guid runId, CancellationToken ct = default);
}

public sealed class AutonomousAcquisitionRunOrchestrator(
    AppDbContext db,
    IAutonomousAcquisitionTemplateRegistry templates,
    IEnumerable<IProspectDiscoveryProvider> providers,
    IAutonomousAcquisitionBackendService backend,
    ITenantContext tenantContext,
    IAutonomousAcquisitionWorkflowPlanner planner) : IAutonomousAcquisitionRunOrchestrator
{
    public async Task ExecuteAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await db.AutonomousAcquisitionAgentRuns
            .SingleOrDefaultAsync(x => x.Id == runId, ct)
            ?? throw new InvalidOperationException("Agent run was not found.");

        if (run.Status != AutonomousAgentRunStatus.Queued)
            return;

        var agent = await db.AutonomousAcquisitionAgents
            .SingleOrDefaultAsync(x => x.Id == run.AgentId && x.TenantId == run.TenantId, ct)
            ?? throw new InvalidOperationException("Agent was not found.");

        if (agent.Status is AutonomousAgentStatus.Paused or AutonomousAgentStatus.Stopped)
            throw new InvalidOperationException("Agent is not allowed to run.");

        if (tenantContext.Current?.Id != run.TenantId)
            throw new InvalidOperationException("Autonomous acquisition run must execute inside its tenant context.");

        run.Status = AutonomousAgentRunStatus.Running;
        run.StartedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        try
        {
            var template = templates.Apply(agent);
            var tasks = await planner.EnsurePlanAsync(agent, template, ct);
            var discoveryTask = tasks.First(x => x.Type == AutonomousAgentTaskTypes.Discover);
            discoveryTask.Status = AutonomousAgentTaskStatus.Running;
            discoveryTask.AttemptCount++;
            discoveryTask.StartedAtUtc = DateTime.UtcNow;
            discoveryTask.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            var countries = ReadCountries(agent.CountriesJson);
            var country = countries.Count == 0
                ? string.Empty
                : countries[(int)(DateTime.UtcNow.DayOfYear % countries.Count)];

            run.Query = await backend.SelectNextQueryAsync(agent, template, ct);

            var icp = new IcpProfile
            {
                TenantId = agent.TenantId,
                Name = $"Agent {agent.Name} run",
                Industry = string.IsNullOrWhiteSpace(agent.Industry) ? template.Industry : agent.Industry,
                CountriesCsv = string.Join(',', countries),
                IntentKeywordsCsv = string.Join(',', template.Keywords),
                Active = true
            };

            var provider = await SelectConfiguredProviderAsync(agent.TenantId, ct)
                ?? throw new InvalidOperationException("No configured prospect discovery provider is available.");

            var candidates = await provider.SearchAsync(
                icp,
                new DiscoveryRunOptions(
                    provider.Name,
                    string.IsNullOrWhiteSpace(agent.Region) ? template.Region : agent.Region,
                    Math.Clamp(agent.DailyDiscoveryLimit, 1, 100),
                    0,
                    CreateTargetList: false,
                    TenantId: agent.TenantId),
                ct);

            run.DiscoveredCount = candidates.Count;

            var existing = await db.Prospects
                .Where(x => x.TenantId == agent.TenantId)
                .Select(x => x.Domain)
                .ToListAsync(ct);

            var known = new HashSet<string>(
                existing.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Normalize),
                StringComparer.OrdinalIgnoreCase);

            var now = DateTime.UtcNow;

            foreach (var candidate in candidates)
            {
                var domain = Normalize(candidate.Domain);

                if (string.IsNullOrWhiteSpace(domain) || known.Contains(domain))
                    continue;

                known.Add(domain);

                var prospect = new Prospect
                {
                    TenantId = agent.TenantId,
                    CompanyName = candidate.CompanyName,
                    Domain = domain,
                    Industry = candidate.Industry ?? agent.Industry,
                    Country = string.IsNullOrWhiteSpace(candidate.Country) ? country : candidate.Country,
                    Source = provider.Name,
                    SourceUrl = candidate.SourceUrl,
                    DatasetOrigin = "autonomous-agent",
                    VerificationStatus = "public-source",
                    ContactReadiness = "company-only",
                    SizeBand = "unknown",
                    Status = ProspectStatus.Discovered,
                    SuggestedBuyer = "Needs enrichment",
                    Priority = "medium",
                    OutreachStatus = "not-ready",
                    PainHypothesis = "Autonomous agent evidence-based research pending.",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                };

                db.Prospects.Add(prospect);
                db.ProspectSignals.Add(new ProspectSignal
                {
                    TenantId = agent.TenantId,
                    ProspectId = prospect.Id,
                    Type = "autonomous-discovery",
                    Source = provider.Name,
                    Evidence = candidate.Evidence,
                    Score = 0,
                    SourceUrl = candidate.SourceUrl,
                    ObservedAtUtc = now
                });
            }

            discoveryTask.Status = AutonomousAgentTaskStatus.Completed;
            discoveryTask.ResultJson = JsonSerializer.Serialize(new { discovered = run.DiscoveredCount, nextTask = AutonomousAgentTaskTypes.Qualify });
            discoveryTask.CompletedAtUtc = DateTime.UtcNow;
            discoveryTask.UpdatedAtUtc = DateTime.UtcNow;
            agent.LastRunAtUtc = now;
            agent.UpdatedAtUtc = now;
            run.Status = AutonomousAgentRunStatus.Completed;
            run.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            var failedTask = await db.AutonomousAcquisitionTasks.FirstOrDefaultAsync(x => x.TenantId == run.TenantId && x.AgentId == run.AgentId && x.Status == AutonomousAgentTaskStatus.Running, CancellationToken.None);
            if (failedTask is not null)
            {
                failedTask.Status = AutonomousAgentTaskStatus.Failed;
                failedTask.Error = ex.Message;
                failedTask.CompletedAtUtc = DateTime.UtcNow;
                failedTask.UpdatedAtUtc = DateTime.UtcNow;
            }
            run.Status = AutonomousAgentRunStatus.Failed;
            run.Error = ex.Message;
            run.CompletedAtUtc = DateTime.UtcNow;
            agent.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<IProspectDiscoveryProvider?> SelectConfiguredProviderAsync(
        Guid tenantId,
        CancellationToken ct)
    {
        foreach (var candidate in providers)
        {
            if (candidate.IsConfigured)
                return candidate;
        }

        return null;
    }

    private static List<string> ReadCountries(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim()
                .ToLowerInvariant()
                .Replace("https://", string.Empty)
                .Replace("http://", string.Empty)
                .Trim('/')
                .Replace("www.", string.Empty);
}
