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
        var run = await db.AutonomousAcquisitionAgentRuns.SingleOrDefaultAsync(x => x.Id == runId, ct)
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
            var now = DateTime.UtcNow;

            await ExecuteDiscoveryAsync(run, agent, template, tasks, now, ct);
            await ExecuteQualificationAsync(run, agent, tasks, ct);
            await ExecuteEnrichmentAsync(run, agent, tasks, ct);
            await ExecuteTargetListAsync(run, agent, tasks, ct);
            var awaitingApproval = await PrepareOutreachAsync(run, agent, template, tasks, ct);

            agent.LastRunAtUtc = now;
            agent.UpdatedAtUtc = now;

            if (awaitingApproval)
            {
                run.Status = AutonomousAgentRunStatus.WaitingApproval;
                run.CompletedAtUtc = null;
            }
            else
            {
                run.Status = AutonomousAgentRunStatus.Completed;
                run.CompletedAtUtc = DateTime.UtcNow;
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            var runningTask = await db.AutonomousAcquisitionTasks
                .FirstOrDefaultAsync(x => x.TenantId == run.TenantId &&
                                           x.AgentId == run.AgentId &&
                                           x.Status == AutonomousAcquisitionTaskStatus.Running,
                    CancellationToken.None);

            if (runningTask is not null)
            {
                runningTask.Status = AutonomousAcquisitionTaskStatus.Failed;
                runningTask.Error = ex.Message;
                runningTask.CompletedAtUtc = DateTime.UtcNow;
                runningTask.UpdatedAtUtc = DateTime.UtcNow;
            }

            run.Status = AutonomousAgentRunStatus.Failed;
            run.Error = ex.Message;
            run.CompletedAtUtc = DateTime.UtcNow;
            agent.Status = AutonomousAgentStatus.Failed;
            agent.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task ExecuteDiscoveryAsync(
        AutonomousAcquisitionAgentRun run,
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        IReadOnlyList<AutonomousAcquisitionTask> tasks,
        DateTime now,
        CancellationToken ct)
    {
        var task = StartTask(tasks, AutonomousAgentTaskTypes.Discover);
        var countries = ReadCountries(agent.CountriesJson);
        var country = countries.Count == 0
            ? string.Empty
            : countries[(int)(DateTime.UtcNow.Ticks % countries.Count)];

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

        var provider = providers.FirstOrDefault(x => x.IsConfigured)
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

        var existing = await db.Prospects.Where(x => x.TenantId == agent.TenantId)
            .Select(x => x.Domain)
            .ToListAsync(ct);

        var known = new HashSet<string>(
            existing.Where(x => !string.IsNullOrWhiteSpace(x)).Select(Normalize),
            StringComparer.OrdinalIgnoreCase);

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
                PainHypothesis = "Evidence-based research pending.",
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

        await db.SaveChangesAsync(ct);
        CompleteTask(task, new { discovered = run.DiscoveredCount, next = AutonomousAgentTaskTypes.Qualify });
    }

    private async Task ExecuteQualificationAsync(
        AutonomousAcquisitionAgentRun run,
        AutonomousAcquisitionAgent agent,
        IReadOnlyList<AutonomousAcquisitionTask> tasks,
        CancellationToken ct)
    {
        var task = StartTask(tasks, AutonomousAgentTaskTypes.Qualify);
        var since = run.StartedAtUtc ?? DateTime.UtcNow;
        var prospects = await db.Prospects
            .Where(x => x.TenantId == agent.TenantId && x.CreatedAtUtc >= since)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Clamp(agent.DailyDiscoveryLimit, 1, 100))
            .ToListAsync(ct);

        foreach (var prospect in prospects)
        {
            var result = await backend.ResearchAsync(agent.TenantId, agent.Id, prospect, agent.MinimumScore, ct);
            if (result.Qualified) run.QualifiedCount++;
            if (result.Score >= agent.MinimumScore) run.HighScoreCount++;
        }

        CompleteTask(task, new { processed = prospects.Count, qualified = run.QualifiedCount, threshold = agent.MinimumScore });
        await db.SaveChangesAsync(ct);
    }

    private async Task ExecuteEnrichmentAsync(
        AutonomousAcquisitionAgentRun run,
        AutonomousAcquisitionAgent agent,
        IReadOnlyList<AutonomousAcquisitionTask> tasks,
        CancellationToken ct)
    {
        var task = StartTask(tasks, AutonomousAgentTaskTypes.Enrich);
        var since = run.StartedAtUtc ?? DateTime.UtcNow;
        var count = await db.Prospects.CountAsync(x =>
            x.TenantId == agent.TenantId &&
            x.CreatedAtUtc >= since &&
            x.Status == ProspectStatus.Qualified, ct);

        CompleteTask(task, new
        {
            enriched = count,
            mode = "evidence-preserving",
            note = "Company enrichment is currently based on discovered public evidence; no contact data is fabricated."
        });
    }

    private async Task ExecuteTargetListAsync(
        AutonomousAcquisitionAgentRun run,
        AutonomousAcquisitionAgent agent,
        IReadOnlyList<AutonomousAcquisitionTask> tasks,
        CancellationToken ct)
    {
        var task = StartTask(tasks, AutonomousAgentTaskTypes.BuildTargetList);
        var campaign = await db.Campaigns
            .SingleOrDefaultAsync(x => x.TenantId == agent.TenantId && x.AgentId == agent.Id, ct);

        if (campaign is null)
        {
            CompleteTask(task, new { added = 0, note = "No campaign container is linked to this agent." });
            return;
        }

        var since = run.StartedAtUtc ?? DateTime.UtcNow;
        var prospects = await db.Prospects
            .Where(x => x.TenantId == agent.TenantId &&
                        x.CreatedAtUtc >= since &&
                        x.Status == ProspectStatus.Qualified)
            .ToListAsync(ct);

        var existing = await db.TargetListMembers
            .Where(x => x.TenantId == agent.TenantId && x.TargetListId == campaign.TargetListId)
            .Select(x => x.ProspectId)
            .ToListAsync(ct);

        var known = existing.ToHashSet();
        var added = 0;
        foreach (var prospect in prospects)
        {
            if (!known.Add(prospect.Id)) continue;
            db.TargetListMembers.Add(new TargetListMember
            {
                TenantId = agent.TenantId,
                TargetListId = campaign.TargetListId,
                ProspectId = prospect.Id,
                AddedAtUtc = DateTime.UtcNow
            });
            added++;
        }

        await db.SaveChangesAsync(ct);
        CompleteTask(task, new { added, targetListId = campaign.TargetListId });
    }

    private async Task<bool> PrepareOutreachAsync(
        AutonomousAcquisitionAgentRun run,
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        IReadOnlyList<AutonomousAcquisitionTask> tasks,
        CancellationToken ct)
    {
        var task = StartTask(tasks, AutonomousAgentTaskTypes.Outreach);
        var campaign = await db.Campaigns
            .SingleOrDefaultAsync(x => x.TenantId == agent.TenantId && x.AgentId == agent.Id, ct);

        if (campaign is null)
        {
            CompleteTask(task, new { prepared = 0, note = "No campaign container is linked to this agent." });
            return false;
        }

        var existingSteps = await db.CampaignSteps
            .Where(x => x.TenantId == agent.TenantId && x.CampaignId == campaign.Id)
            .ToListAsync(ct);

        foreach (var message in template.OutreachTemplates.OrderBy(x => x.Step))
        {
            var step = existingSteps.FirstOrDefault(x => x.StepNumber == message.Step);
            if (step is null)
            {
                db.CampaignSteps.Add(new CampaignStep
                {
                    TenantId = agent.TenantId,
                    CampaignId = campaign.Id,
                    StepNumber = message.Step,
                    DelayHours = message.DelayHours,
                    Channel = "email",
                    SubjectTemplate = message.Subject,
                    BodyTemplate = message.Body,
                    RulesJson = JsonSerializer.Serialize(new { requiresApproval = message.RequiresApproval })
                });
            }
        }

        var members = await db.TargetListMembers
            .Where(x => x.TenantId == agent.TenantId && x.TargetListId == campaign.TargetListId)
            .Select(x => x.ProspectId)
            .ToListAsync(ct);

        var existingRecipients = await db.CampaignRecipients
            .Where(x => x.TenantId == agent.TenantId && x.CampaignId == campaign.Id)
            .Select(x => x.ProspectId)
            .ToListAsync(ct);

        var knownRecipients = existingRecipients.ToHashSet();
        var prepared = 0;

        foreach (var prospectId in members)
        {
            if (!knownRecipients.Add(prospectId)) continue;
            var prospect = await db.Prospects.SingleOrDefaultAsync(x => x.TenantId == agent.TenantId && x.Id == prospectId, ct);
            if (prospect is null || prospect.Status != ProspectStatus.Qualified) continue;
            if (!await backend.CanContactAsync(agent.TenantId, prospect, agent.DailyEmailLimit, ct)) continue;

            db.CampaignRecipients.Add(new CampaignRecipient
            {
                TenantId = agent.TenantId,
                CampaignId = campaign.Id,
                ProspectId = prospect.Id,
                CurrentStep = 1,
                Status = "pending-approval",
                NextRunAtUtc = null
            });
            prepared++;
        }

        await db.SaveChangesAsync(ct);

        var requiresApproval = template.OutreachTemplates.Any(x => x.RequiresApproval);
        if (requiresApproval)
        {
            CompleteTask(task, new { prepared, requiresApproval = true, next = "campaign approval" });
            return prepared > 0;
        }

        CompleteTask(task, new { prepared, requiresApproval = false });
        return false;
    }

    private static AutonomousAcquisitionTask StartTask(
        IReadOnlyList<AutonomousAcquisitionTask> tasks,
        string type)
    {
        var task = tasks.First(x => x.Type == type);
        task.Status = AutonomousAcquisitionTaskStatus.Running;
        task.AttemptCount++;
        task.StartedAtUtc = DateTime.UtcNow;
        task.CompletedAtUtc = null;
        task.Error = null;
        task.UpdatedAtUtc = DateTime.UtcNow;
        return task;
    }

    private static void CompleteTask(AutonomousAcquisitionTask task, object result)
    {
        task.Status = AutonomousAcquisitionTaskStatus.Completed;
        task.ResultJson = JsonSerializer.Serialize(result);
        task.CompletedAtUtc = DateTime.UtcNow;
        task.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static List<string> ReadCountries(string json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json) ?? []; }
        catch { return []; }
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToLowerInvariant()
                .Replace("https://", string.Empty)
                .Replace("http://", string.Empty)
                .Trim('/')
                .Replace("www.", string.Empty);
}