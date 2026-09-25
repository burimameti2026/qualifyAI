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

        if (run.Status is not (AutonomousAgentRunStatus.Queued or AutonomousAgentRunStatus.WaitingApproval))
            return;

        var campaign = await db.Campaigns
            .SingleOrDefaultAsync(x => x.TenantId == run.TenantId && x.Id == run.CampaignId, ct)
            ?? throw new InvalidOperationException("Campaign container was not found for the acquisition run.");

        if (campaign.Status is not CampaignStatus.Running)
        {
            run.Status = campaign.Status == CampaignStatus.Paused
                ? AutonomousAgentRunStatus.Paused
                : AutonomousAgentRunStatus.Cancelled;
            await db.SaveChangesAsync(ct);
            return;
        }

        var agent = await db.AutonomousAcquisitionAgents
            .SingleOrDefaultAsync(x => x.Id == run.AgentId && x.TenantId == run.TenantId, ct)
            ?? throw new InvalidOperationException("Agent was not found.");

        if (agent.Status is AutonomousAgentStatus.Paused)
        {
            run.Status = AutonomousAgentRunStatus.Paused;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (agent.Status is AutonomousAgentStatus.Stopped)
        {
            run.Status = AutonomousAgentRunStatus.Cancelled;
            run.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return;
        }

        if (agent.Status is not AutonomousAgentStatus.Active)
            throw new InvalidOperationException("Only active agents are allowed to run.");

        if (tenantContext.Current?.Id != run.TenantId)
            throw new InvalidOperationException("Autonomous acquisition run must execute inside its tenant context.");

        var claimed = await db.AutonomousAcquisitionAgentRuns
            .Where(x => x.TenantId == run.TenantId &&
                        x.Id == run.Id &&
                        (x.Status == AutonomousAgentRunStatus.Queued ||
                         x.Status == AutonomousAgentRunStatus.WaitingApproval))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, AutonomousAgentRunStatus.Running)
                .SetProperty(x => x.StartedAtUtc, x => x.StartedAtUtc ?? DateTime.UtcNow)
                .SetProperty(x => x.Error, (string?)null), ct);

        if (claimed == 0)
            return;

        run.Status = AutonomousAgentRunStatus.Running;
        run.StartedAtUtc ??= DateTime.UtcNow;

        try
        {
            var template = templates.Apply(agent);
            await planner.EnsurePlanAsync(agent, template, ct);
            var tasks = await EnsureRunTasksAsync(run, agent, template, ct);
            var now = DateTime.UtcNow;

            var awaitingApproval = false;
            var current = tasks
                .Where(x => x.Status != AutonomousAgentTaskStatus.Completed)
                .OrderBy(x => x.Sequence)
                .FirstOrDefault();

            var visited = new HashSet<Guid>();
            while (current is not null && visited.Add(current.Id))
            {
                if (!await CanContinueAsync(run, agent, ct))
                    return;

                awaitingApproval = await ExecuteTaskAsync(current, run, agent, template, tasks, now, ct);
                if (awaitingApproval)
                    break;

                var nextType = ReadNextStep(current.ConfigurationJson);
                current = string.IsNullOrWhiteSpace(nextType)
                    ? tasks.Where(x => x.Status != AutonomousAgentTaskStatus.Completed).OrderBy(x => x.Sequence).FirstOrDefault()
                    : tasks.FirstOrDefault(x => x.Type == nextType && x.Status != AutonomousAgentTaskStatus.Completed);
            }

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
                                           x.RunId == run.Id &&
                                           x.Status == AutonomousAgentTaskStatus.Running,
                    CancellationToken.None);

            if (runningTask is not null)
            {
                runningTask.Status = AutonomousAgentTaskStatus.Failed;
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

    private static string ReadNextStep(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return doc.RootElement.TryGetProperty("nextStep", out var value)
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }
        catch { return string.Empty; }
    }

    private async Task<bool> ExecuteTaskAsync(
        AutonomousAcquisitionTask task,
        AutonomousAcquisitionAgentRun run,
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        IReadOnlyList<AutonomousAcquisitionTask> tasks,
        DateTime now,
        CancellationToken ct)
        => task.Type switch
        {
            AutonomousAgentTaskTypes.Discover => await ExecuteDiscoveryAndContinueAsync(task, run, agent, template, tasks, now, ct),
            AutonomousAgentTaskTypes.Qualify => await ExecuteQualificationAndContinueAsync(task, run, agent, tasks, ct),
            AutonomousAgentTaskTypes.Enrich => await ExecuteEnrichmentAndContinueAsync(task, run, agent, tasks, ct),
            AutonomousAgentTaskTypes.BuildTargetList => await ExecuteTargetListAndContinueAsync(task, run, agent, tasks, ct),
            AutonomousAgentTaskTypes.Outreach => await PrepareOutreachAsync(run, agent, template, tasks, ct),
            _ => throw new InvalidOperationException($"No executor is registered for acquisition task '{task.Type}'.")
        };

    private async Task<bool> ExecuteDiscoveryAndContinueAsync(AutonomousAcquisitionTask task, AutonomousAcquisitionAgentRun run, AutonomousAcquisitionAgent agent, AutonomousAcquisitionTemplate templateAgentTemplate, IReadOnlyList<AutonomousAcquisitionTask> tasks, DateTime now, CancellationToken ct)
    {
        await ExecuteDiscoveryAsync(run, agent, templateAgentTemplate, tasks, now, ct);
        return false;
    }

    private async Task<bool> ExecuteQualificationAndContinueAsync(AutonomousAcquisitionTask task, AutonomousAcquisitionAgentRun run, AutonomousAcquisitionAgent agent, IReadOnlyList<AutonomousAcquisitionTask> tasks, CancellationToken ct)
    {
        await ExecuteQualificationAsync(run, agent, tasks, ct);
        return false;
    }

    private async Task<bool> ExecuteEnrichmentAndContinueAsync(AutonomousAcquisitionTask task, AutonomousAcquisitionAgentRun run, AutonomousAcquisitionAgent agent, IReadOnlyList<AutonomousAcquisitionTask> tasks, CancellationToken ct)
    {
        await ExecuteEnrichmentAsync(run, agent, tasks, ct);
        return false;
    }

    private async Task<bool> ExecuteTargetListAndContinueAsync(AutonomousAcquisitionTask task, AutonomousAcquisitionAgentRun run, AutonomousAcquisitionAgent agent, IReadOnlyList<AutonomousAcquisitionTask> tasks, CancellationToken ct)
    {
        await ExecuteTargetListAsync(run, agent, tasks, ct);
        return false;
    }

    private async Task<bool> CanContinueAsync(AutonomousAcquisitionAgentRun run, AutonomousAcquisitionAgent agent, CancellationToken ct)
    {
        var campaign = await db.Campaigns.SingleAsync(x => x.TenantId == run.TenantId && x.Id == run.CampaignId, ct);
        if (campaign.Status == CampaignStatus.Paused || agent.Status == AutonomousAgentStatus.Paused)
        {
            run.Status = AutonomousAgentRunStatus.Paused;
            run.CompletedAtUtc = null;
            await db.SaveChangesAsync(ct);
            return false;
        }

        if (campaign.Status is CampaignStatus.Stopped or CampaignStatus.Completed || agent.Status is AutonomousAgentStatus.Stopped)
        {
            run.Status = AutonomousAgentRunStatus.Cancelled;
            run.CompletedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return false;
        }

        return campaign.Status == CampaignStatus.Running && agent.Status == AutonomousAgentStatus.Active;
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

        var campaign = await db.Campaigns
            .SingleOrDefaultAsync(x => x.TenantId == run.TenantId && x.Id == run.CampaignId, ct)
            ?? throw new InvalidOperationException("Campaign container was not found for the acquisition run.");

        var icpId = await db.TargetLists
            .Where(x => x.TenantId == run.TenantId && x.Id == campaign.TargetListId)
            .Select(x => x.IcpProfileId)
            .FirstOrDefaultAsync(ct);

        var icp = icpId.HasValue
            ? await db.IcpProfiles.SingleOrDefaultAsync(x => x.TenantId == run.TenantId && x.Id == icpId.Value && x.Active, ct)
            : null;

        icp ??= new IcpProfile
        {
            TenantId = agent.TenantId,
            Name = $"{campaign.Name} ICP",
            Industry = string.IsNullOrWhiteSpace(agent.Industry) ? template.Industry : agent.Industry,
            CountriesCsv = string.Join(',', countries),
            IntentKeywordsCsv = string.Join(',', template.Keywords),
            CriteriaJson = agent.IcpJson,
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
                DatasetOrigin = $"autonomous-agent:{run.CampaignId:N}",
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
            .Where(x => x.TenantId == agent.TenantId && x.CreatedAtUtc >= since && x.DatasetOrigin == $"autonomous-agent:{run.CampaignId:N}")
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
            x.DatasetOrigin == $"autonomous-agent:{run.CampaignId:N}" &&
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
            .SingleOrDefaultAsync(x => x.TenantId == agent.TenantId && x.Id == run.CampaignId, ct);

        if (campaign is null)
        {
            CompleteTask(task, new { added = 0, note = "No campaign container is linked to this agent." });
            return;
        }

        var since = run.StartedAtUtc ?? DateTime.UtcNow;
        var prospects = await db.Prospects
            .Where(x => x.TenantId == agent.TenantId &&
                        x.CreatedAtUtc >= since &&
                        x.DatasetOrigin == $"autonomous-agent:{run.CampaignId:N}" &&
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
            .SingleOrDefaultAsync(x => x.TenantId == agent.TenantId && x.Id == run.CampaignId, ct);

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
                    RulesJson = JsonSerializer.Serialize(new { requiresApproval = true })
                });
            }
        }

        await db.SaveChangesAsync(ct);

        existingSteps = await db.CampaignSteps
            .Where(x => x.TenantId == agent.TenantId && x.CampaignId == campaign.Id)
            .ToListAsync(ct);

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
        var firstTemplate = template.OutreachTemplates.OrderBy(x => x.Step).FirstOrDefault();

        if (firstTemplate is null)
        {
            CompleteTask(task, new { prepared = 0, requiresApproval = true, next = "no outreach template configured" });
            return false;
        }

        foreach (var prospectId in members)
        {
            if (!knownRecipients.Add(prospectId)) continue;
            var prospect = await db.Prospects.SingleOrDefaultAsync(
                x => x.TenantId == agent.TenantId && x.Id == prospectId,
                ct);
            if (prospect is null || prospect.Status != ProspectStatus.Qualified) continue;
            if (!await backend.CanContactAsync(agent.TenantId, prospect, agent.DailyEmailLimit, ct)) continue;

            var recipient = new CampaignRecipient
            {
                TenantId = agent.TenantId,
                CampaignId = campaign.Id,
                ProspectId = prospect.Id,
                CurrentStep = firstTemplate.Step,
                Status = "pending-approval",
                NextRunAtUtc = null
            };
            db.CampaignRecipients.Add(recipient);

            var step = existingSteps.FirstOrDefault(x => x.StepNumber == firstTemplate.Step);
            if (step is null) continue;

            var message = new OutreachMessage
            {
                TenantId = agent.TenantId,
                CampaignId = campaign.Id,
                ProspectId = prospect.Id,
                CampaignStepId = step.Id,
                Channel = step.Channel,
                Subject = CampaignExecutionService.RenderTemplate(step.SubjectTemplate, prospect),
                Body = CampaignExecutionService.RenderTemplate(step.BodyTemplate, prospect),
                Status = OutreachStatus.Queued
            };
            db.OutreachMessages.Add(message);

            var approvalTitle = $"APPROVAL: Send outreach {message.Id}";
            db.CrmTasks.Add(new CrmTask
            {
                TenantId = agent.TenantId,
                Title = approvalTitle,
                DueAtUtc = DateTime.UtcNow.AddHours(4)
            });

            prepared++;
        }

        await db.SaveChangesAsync(ct);

        CompleteTask(task, new
        {
            prepared,
            requiresApproval = true,
            next = "campaign approval"
        });
        return prepared > 0;
    }

    private async Task<IReadOnlyList<AutonomousAcquisitionTask>> EnsureRunTasksAsync(
        AutonomousAcquisitionAgentRun run,
        AutonomousAcquisitionAgent agent,
        AutonomousAcquisitionTemplate template,
        CancellationToken ct)
    {
        var existing = await db.AutonomousAcquisitionTasks
            .Where(x => x.TenantId == run.TenantId && x.AgentId == agent.Id && x.RunId == run.Id)
            .OrderBy(x => x.Sequence)
            .ToListAsync(ct);
        if (existing.Count > 0) return existing;

        var definitions = await planner.EnsurePlanAsync(agent, template, ct);
        var instances = definitions.Select(d => new AutonomousAcquisitionTask
        {
            TenantId = run.TenantId,
            AgentId = agent.Id,
            RunId = run.Id,
            Sequence = d.Sequence,
            Type = d.Type,
            Name = d.Name,
            Status = AutonomousAgentTaskStatus.Pending,
            RequiresApproval = d.RequiresApproval,
            ConfigurationJson = d.ConfigurationJson,
            ResultJson = "{}"
        }).ToList();
        db.AutonomousAcquisitionTasks.AddRange(instances);
        await db.SaveChangesAsync(ct);
        return instances;
    }

    private async Task<bool> HasPendingApprovalAsync(AutonomousAcquisitionAgentRun run, AutonomousAcquisitionAgent agent, CancellationToken ct)
    {
        var campaign = await db.Campaigns.SingleOrDefaultAsync(x => x.TenantId == agent.TenantId && x.Id == run.CampaignId, ct);
        if (campaign is null) return false;
        return await db.OutreachMessages.AnyAsync(x =>
            x.TenantId == run.TenantId && x.CampaignId == campaign.Id && x.Status == OutreachStatus.Queued &&
            db.CrmTasks.Any(t => t.TenantId == run.TenantId && t.Title == $"APPROVAL: Send outreach {x.Id}" && !t.Completed), ct);
    }

    private static bool IsCompleted(IReadOnlyList<AutonomousAcquisitionTask> tasks, string type) =>
        tasks.Any(x => x.Type == type && x.Status == AutonomousAgentTaskStatus.Completed);

    private static AutonomousAcquisitionTask StartTask(
        IReadOnlyList<AutonomousAcquisitionTask> tasks,
        string type)
    {
        var task = tasks.First(x => x.Type == type);
        task.Status = AutonomousAgentTaskStatus.Running;
        task.AttemptCount++;
        task.StartedAtUtc = DateTime.UtcNow;
        task.CompletedAtUtc = null;
        task.Error = null;
        task.UpdatedAtUtc = DateTime.UtcNow;
        return task;
    }

    private static void CompleteTask(AutonomousAcquisitionTask task, object result)
    {
        task.Status = AutonomousAgentTaskStatus.Completed;
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