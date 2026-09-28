using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Infrastructure.Acquisition;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/acquisition")]
public sealed class AcquisitionController(
    AppDbContext db,
    ITenantContext tenant,
    CampaignExecutionService executor,
    ProspectReplyProcessingService replyProcessor,
    ProspectDiscoveryService discovery,
    IAgentJobFactory jobFactory) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("overview")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var id = TenantId;
        return Ok(new
        {
            discovered = await db.Prospects.CountAsync(x => x.TenantId==id, ct),
            hot = await db.Prospects.CountAsync(x => x.TenantId==id&&x.FitScore*55+x.IntentScore*45>=7500, ct),
            activeCampaigns = await db.Campaigns.CountAsync(x => x.TenantId==id&&x.Status==CampaignStatus.Running, ct),
            queuedMessages = await db.OutreachMessages.CountAsync(x => x.TenantId==id&&x.Status==OutreachStatus.Queued, ct),
            replies = await db.ProspectReplies.CountAsync(x => x.TenantId==id, ct),
            demoReady = await db.Prospects.CountAsync(x => x.TenantId==id&&x.Status==ProspectStatus.DemoReady, ct)
        });
    }

    [HttpGet("icp")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Icp(CancellationToken ct)
    {
        var rows = await db.IcpProfiles.AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return Ok(rows.Select(x => new
        {
            x.Id,
            x.TenantId,
            x.Name,
            x.Industry,
            x.CountriesCsv,
            x.MinimumEmployees,
            x.MaximumEmployees,
            x.IntentKeywordsCsv,
            x.CriteriaJson,
            x.Active,
            x.LastDiscoveryAtUtc,
            minimumScore = ReadMinimumScore(x.CriteriaJson)
        }));
    }

    [HttpPost("icp")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SaveIcp([FromBody] IcpSaveRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { error = "ICP name is required." });

        var tenantId = TenantId;
        IcpProfile? profile = null;
        if (input.Id.HasValue && input.Id.Value != Guid.Empty)
            profile = await db.IcpProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == input.Id.Value, ct);

        var isNew = profile is null;
        profile ??= new IcpProfile { Id = Guid.NewGuid(), TenantId = tenantId };

        profile.Name = input.Name.Trim();
        profile.Industry = input.Industry?.Trim() ?? string.Empty;
        profile.CountriesCsv = input.CountriesCsv?.Trim() ?? string.Empty;
        profile.IntentKeywordsCsv = input.IntentKeywordsCsv?.Trim() ?? string.Empty;
        profile.MinimumEmployees = input.MinimumEmployees;
        profile.MaximumEmployees = input.MaximumEmployees;
        profile.CriteriaJson = NormalizeCriteria(input.CriteriaJson, input.MinimumScore);
        profile.Active = input.Active;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        if (isNew)
            db.IcpProfiles.Add(profile);

        await db.SaveChangesAsync(ct);
        return Ok(new
        {
            profile.Id,
            profile.TenantId,
            profile.Name,
            profile.Industry,
            profile.CountriesCsv,
            profile.MinimumEmployees,
            profile.MaximumEmployees,
            profile.IntentKeywordsCsv,
            profile.CriteriaJson,
            profile.Active,
            profile.LastDiscoveryAtUtc,
            minimumScore = ReadMinimumScore(profile.CriteriaJson)
        });
    }

    [HttpGet("prospects")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Prospects([FromQuery] int minimumScore = 0, CancellationToken ct = default)
    {
        var threshold = Math.Clamp(minimumScore, 0, 100) * 100;
        var rows = await db.Prospects
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.FitScore * 55 + x.IntentScore * 45 >= threshold)
            .OrderByDescending(x => x.FitScore * 55 + x.IntentScore * 45)
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("target-lists")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> TargetLists(CancellationToken ct)
    {
        var rows = await db.TargetLists
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpGet("discovery/providers")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public IActionResult DiscoveryProviders() => Ok(discovery.ProviderStatus());

    [HttpPost("icp/{id:guid}/discover")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Discover(Guid id, [FromBody] DiscoveryRequest? input, CancellationToken ct)
    {
        try
        {
            var request = input ?? new DiscoveryRequest();
            var result = await discovery.DiscoverAsync(TenantId, id, new DiscoveryRunOptions(
                request.Source, request.Region, request.MaximumResults, request.MinimumScore,
                request.TargetListName, request.CreateTargetList), ct);
            return Ok(result);
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { code = "discovery_not_ready", detail = exception.Message });
        }
    }

    [HttpPost("discovery/providers/{name}/verify")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> VerifyDiscoveryProvider(string name, CancellationToken ct)
    {
        try
        {
            var result = await discovery.VerifyProviderAsync(name, ct);
            return result.Verified
                ? Ok(result)
                : BadRequest(result);
        }
        catch (InvalidOperationException exception)
        {
            return NotFound(new
            {
                code = "discovery_provider_not_found",
                detail = exception.Message
            });
        }
    }

    [HttpGet("campaigns")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Campaigns(CancellationToken ct)
    {
        var tenantId = TenantId;
        var campaigns = await db.Campaigns.AsNoTracking().Where(x => x.TenantId==tenantId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(campaign => new
            {
                campaign.Id,
                campaign.TargetListId,
                campaign.Name,
                campaign.Goal,
                campaign.Objective,
                campaign.Status,
                campaign.SenderName,
                campaign.SenderEmail,
                campaign.StartsAtUtc,
                campaign.CreatedAtUtc,
                campaign.UpdatedAtUtc,
                campaign.PackageCode,
                campaign.PackageVersion,
                campaign.PlanStatus,
                campaign.PlanJson,
                campaign.AgentId,
                recipients = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id),
                active = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="active"),
                awaitingDelivery = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="awaiting-delivery"),
                replied = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="replied"),
                completed = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="completed"),
                failed = db.CampaignRecipients.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status=="failed"),
                queued = db.OutreachMessages.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status==OutreachStatus.Queued),
                sent = db.OutreachMessages.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&(x.Status==OutreachStatus.Sent||x.Status==OutreachStatus.Delivered||x.Status==OutreachStatus.Replied)),
                runs = db.AgentJobs.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id),
                runSuccess = db.AgentJobs.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status==AgentJobStatus.Completed),
                runFailed = db.AgentJobs.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status==AgentJobStatus.Failed),
                runPending = db.AgentJobs.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&(x.Status==AgentJobStatus.Queued||x.Status==AgentJobStatus.Waiting)),
                runRunning = db.AgentJobs.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status==AgentJobStatus.Running)
            }).ToListAsync(ct);
        return Ok(campaigns);
    }

    [HttpGet("campaign-history")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> CampaignHistory(
        [FromQuery] string? campaignName,
        [FromQuery] string? containerName,
        [FromQuery] string? agentName,
        [FromQuery] string? packCode,
        [FromQuery] string? status,
        [FromQuery] string? stepType,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int take = 500,
        CancellationToken ct = default)
    {
        var tenantId = TenantId;
        take = Math.Clamp(take, 1, 2000);

        var campaigns = await db.Campaigns.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.PackageCode,
                x.AgentId
            })
            .ToListAsync(ct);

        var containers = await db.CampaignContainers.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new
            {
                x.Id,
                x.CampaignId,
                x.AgentId,
                x.Name,
                x.PackageCode,
                x.PackageVersion,
                x.Status
            })
            .ToListAsync(ct);

        var agents = await db.AutonomousAcquisitionAgents.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);

        var campaignMap = campaigns.ToDictionary(x => x.Id);
        var containerMap = containers.ToDictionary(x => x.Id);

        var allowedContainerIds = containers
            .Where(x =>
                (string.IsNullOrWhiteSpace(campaignName) ||
                 (campaignMap.TryGetValue(x.CampaignId, out var campaign) &&
                  campaign.Name.Contains(campaignName.Trim()))) &&
                (string.IsNullOrWhiteSpace(containerName) ||
                 x.Name.Contains(containerName.Trim())) &&
                (string.IsNullOrWhiteSpace(packCode) ||
                 x.PackageCode.Contains(packCode.Trim())) &&
                (string.IsNullOrWhiteSpace(agentName) ||
                 (agents.TryGetValue(x.AgentId, out var name) &&
                  name.Contains(agentName.Trim()))))
            .Select(x => x.Id)
            .ToHashSet();

        var logs = await db.AuditLogs.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.EntityType == "CampaignContainerActivity")
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(Math.Min(take * 5, 10000))
            .ToListAsync(ct);

        var items = new List<object>(Math.Min(logs.Count, take));

        foreach (var log in logs)
        {
            if (!Guid.TryParse(log.EntityId, out var containerId) ||
                !allowedContainerIds.Contains(containerId) ||
                !containerMap.TryGetValue(containerId, out var container) ||
                !campaignMap.TryGetValue(container.CampaignId, out var campaign))
                continue;

            if (fromUtc.HasValue && log.CreatedAtUtc < fromUtc.Value) continue;
            if (toUtc.HasValue && log.CreatedAtUtc > toUtc.Value) continue;

            string level = "info";
            string eventStepType = "";
            string stepName = "";
            string message = log.Action;
            string? runId = null;
            string? taskId = null;
            object data = new { };

            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(log.DataJson ?? "{}");
                var root = doc.RootElement;
                if (root.TryGetProperty("level", out var p)) level = p.GetString() ?? level;
                if (root.TryGetProperty("stepType", out p)) eventStepType = p.GetString() ?? "";
                if (root.TryGetProperty("stepName", out p)) stepName = p.GetString() ?? "";
                if (root.TryGetProperty("message", out p)) message = p.GetString() ?? message;
                if (root.TryGetProperty("runId", out p)) runId = p.ToString();
                if (root.TryGetProperty("taskId", out p)) taskId = p.ToString();
                if (root.TryGetProperty("data", out p)) data = p.Clone();
            }
            catch (System.Text.Json.JsonException)
            {
                // Keep the persisted audit event visible even when an older payload is malformed.
            }

            if (!string.IsNullOrWhiteSpace(stepType) &&
                !string.Equals(eventStepType, stepType.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            var eventStatus = level.Equals("error", StringComparison.OrdinalIgnoreCase) ||
                              level.Equals("failed", StringComparison.OrdinalIgnoreCase) ||
                              log.Action.Contains("failed", StringComparison.OrdinalIgnoreCase)
                ? "Failed"
                : log.Action.Contains("waiting", StringComparison.OrdinalIgnoreCase)
                    ? "Waiting"
                    : log.Action.Contains("completed", StringComparison.OrdinalIgnoreCase) ||
                      log.Action.Contains("stopped", StringComparison.OrdinalIgnoreCase)
                        ? "Completed"
                        : log.Action.Contains("started", StringComparison.OrdinalIgnoreCase)
                            ? "Running"
                            : level;

            if (!string.IsNullOrWhiteSpace(status) &&
                !string.Equals(eventStatus, status.Trim(), StringComparison.OrdinalIgnoreCase))
                continue;

            items.Add(new
            {
                id = log.Id,
                atUtc = log.CreatedAtUtc,
                campaignId = campaign.Id,
                campaignName = campaign.Name,
                containerId = container.Id,
                containerName = container.Name,
                agentId = container.AgentId,
                agentName = agents.TryGetValue(container.AgentId, out var agent) ? agent : "Unknown",
                packCode = container.PackageCode,
                packVersion = container.PackageVersion,
                runId,
                taskId,
                stepType = eventStepType,
                stepName,
                eventType = log.Action,
                level,
                status = eventStatus,
                message,
                data
            });

            if (items.Count >= take) break;
        }

        return Ok(new
        {
            items,
            total = items.Count,
            filters = new
            {
                campaigns = campaigns.Select(x => new { x.Id, x.Name }).OrderBy(x => x.Name),
                containers = containers.Select(x => new { x.Id, x.Name, x.CampaignId }).OrderBy(x => x.Name),
                agents = agents.Select(x => new { id = x.Key, name = x.Value }).OrderBy(x => x.name),
                packs = containers.Select(x => x.PackageCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().OrderBy(x => x)
            }
        });
    }

    [HttpGet("campaigns/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> CampaignDetail(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId;
        var campaign = await db.Campaigns.AsNoTracking().Where(x => x.TenantId == tenantId && x.Id == id)
            .Select(x => new { x.Id, x.TargetListId, x.Name, x.Goal, x.Objective, x.Status, x.SenderName, x.SenderEmail, x.StartsAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc, x.PackageCode, x.PackageVersion, x.PlanStatus, x.PlanJson, x.AgentId })
            .SingleOrDefaultAsync(ct);
        if (campaign is null) return NotFound();
        var steps = await db.CampaignSteps.AsNoTracking().Where(x => x.TenantId == tenantId && x.CampaignId == id)
            .OrderBy(x => x.StepNumber)
            .Select(x => new { x.Id, x.StepNumber, x.DelayHours, x.Channel, x.SubjectTemplate, x.BodyTemplate, x.TemplateId })
            .ToListAsync(ct);

        var targetList = await db.TargetLists.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Id == campaign.TargetListId)
            .Select(x => new { x.Id, x.Name, x.Description, x.CampaignId, x.IcpProfileId, x.Dynamic })
            .SingleOrDefaultAsync(ct);

        var icpId = targetList?.IcpProfileId;
        var icp = icpId.HasValue
            ? await db.IcpProfiles.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == icpId.Value)
                .Select(x => new { x.Id, x.Name, x.Industry, x.CountriesCsv, x.IntentKeywordsCsv, x.MinimumEmployees, x.MaximumEmployees, x.CriteriaJson, x.Active })
                .SingleOrDefaultAsync(ct)
            : null;

        var prospects = await (
            from member in db.TargetListMembers.AsNoTracking()
            join prospect in db.Prospects.AsNoTracking() on member.ProspectId equals prospect.Id
            where member.TenantId == tenantId && member.TargetListId == campaign.TargetListId && prospect.TenantId == tenantId
            orderby prospect.FitScore descending, prospect.IntentScore descending, prospect.CompanyName
            select new
            {
                prospect.Id,
                prospect.CompanyName,
                prospect.Domain,
                prospect.ContactName,
                prospect.Email,
                prospect.JobTitle,
                prospect.Industry,
                prospect.Country,
                prospect.Status,
                prospect.FitScore,
                prospect.IntentScore,
                prospect.PriorityScore,
                prospect.Source,
                prospect.UpdatedAtUtc
            }).Take(500).ToListAsync(ct);

        var latestRun = await db.AgentJobs.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CampaignId == id)
            .OrderByDescending(x => x.ScheduledAtUtc)
            .Select(x => new
            {
                x.Id,
                x.Status,
                x.ScheduledAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.DiscoveredCount,
                x.QualifiedCount,
                x.HighScoreCount,
                x.EmailsSentCount,
                x.Error
            })
            .FirstOrDefaultAsync(ct);

        var tasks = latestRun is null
            ? Enumerable.Empty<object>().ToList()
            : (await db.AutonomousAcquisitionTasks.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.RunId == latestRun.Id)
                .OrderBy(x => x.Sequence)
                .Select(x => new
                {
                    x.Id,
                    x.Sequence,
                    x.Type,
                    x.Name,
                    x.Status,
                    x.RequiresApproval,
                    x.StartedAtUtc,
                    x.CompletedAtUtc,
                    x.ConfigurationJson,
                    x.ResultJson,
                    x.Error
                })
                .ToListAsync(ct)).Cast<object>().ToList();

        return Ok(new { campaign, steps, icp, targetList, prospects, latestRun, tasks });
    }

    [HttpPut("campaigns/{id:guid}/plan")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SaveCampaignPlan(Guid id, CampaignPlanRequest input, CancellationToken ct)
    {
        var campaign = await db.Campaigns
            .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);

        if (campaign is null)
            return NotFound();

        if (string.IsNullOrWhiteSpace(input.PlanJson))
            return BadRequest(new { detail = "Campaign plan cannot be empty." });

        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(input.PlanJson);
            if (document.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return BadRequest(new { detail = "Campaign plan must be a JSON object." });

            campaign.PlanJson = document.RootElement.GetRawText();
            campaign.PlanStatus = "ready";
            campaign.UpdatedAtUtc = DateTime.UtcNow;

            await db.SaveChangesAsync(ct);
            return Ok(new { id = campaign.Id, planStatus = campaign.PlanStatus, planJson = campaign.PlanJson });
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new { detail = "Campaign plan contains invalid JSON." });
        }
    }

    [HttpPut("campaigns/{id:guid}/messages")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SaveCampaignMessages(Guid id, CampaignMessagesRequest input, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();
        if (input.Steps is null || input.Steps.Count == 0)
            return BadRequest(new { detail = "At least one outreach message template is required." });

        var steps = input.Steps.OrderBy(x => x.StepNumber).ToList();
        if (steps.Any(x => x.StepNumber <= 0 || string.IsNullOrWhiteSpace(x.SubjectTemplate) || string.IsNullOrWhiteSpace(x.BodyTemplate)))
            return BadRequest(new { detail = "Every message needs a step number, subject and body." });
        if (steps.Select(x => x.StepNumber).Distinct().Count() != steps.Count)
            return BadRequest(new { detail = "Message step numbers must be unique." });

        var existing = await db.CampaignSteps
            .Where(x => x.TenantId == TenantId && x.CampaignId == id)
            .ToListAsync(ct);
        if (existing.Count > 0) db.CampaignSteps.RemoveRange(existing);

        db.CampaignSteps.AddRange(steps.Select(x => new CampaignStep
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CampaignId = id,
            StepNumber = x.StepNumber,
            DelayHours = Math.Max(0, x.DelayHours),
            Channel = string.IsNullOrWhiteSpace(x.Channel) ? "email" : x.Channel.Trim(),
            SubjectTemplate = x.SubjectTemplate.Trim(),
            BodyTemplate = x.BodyTemplate.Trim()
        }));

        campaign.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var saved = await db.CampaignSteps.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.CampaignId == id)
            .OrderBy(x => x.StepNumber)
            .Select(x => new { x.Id, x.StepNumber, x.DelayHours, x.Channel, x.SubjectTemplate, x.BodyTemplate })
            .ToListAsync(ct);
        return Ok(saved);
    }

    [HttpPost("campaigns/{id:guid}/pause")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();

        try
        {
            campaign.Status = CampaignStatus.Paused;

            if (campaign.AgentId.HasValue)
            {
                var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
                    x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value, ct);
                if (agent is not null)
                {
                    agent.Status = AutonomousAgentStatus.Paused;
                    agent.UpdatedAtUtc = DateTime.UtcNow;
                }
            }

await db.SaveChangesAsync(ct);
            return Ok(new { campaign.Id, campaign.Status });
        }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPost("campaigns/{id:guid}/resume")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        Guid? runId = null;
        try
        {
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var campaign = await db.Campaigns
                    .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
                if (campaign is null)
                    throw new KeyNotFoundException($"Campaign '{id}' was not found.");
                campaign.Resume();
                var agentId = campaign.AgentId;
                if (!agentId.HasValue)
                    throw new InvalidOperationException("Campaign agent is not configured.");

                var job = await jobFactory.QueueCampaignAsync(
                    TenantId,
                    campaign.Id,
                    agentId.Value,
                    null,
                    "campaign.execute",
                    $"campaign:{campaign.Id}",
                    true,
                    ct);
                runId = job.Id;

await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });

            return Ok(new { id, status = CampaignStatus.Running, jobId = runId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpGet("campaigns/{id:guid}/containers")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Containers(Guid id, CancellationToken ct)
    {
        var exists = await db.Campaigns.AnyAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (!exists) return NotFound();

        var rows = await db.CampaignContainers.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.CampaignId == id)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x => new
            {
                x.Id,
                x.CampaignId,
                x.AgentId,
                x.Name,
                x.PackageCode,
                x.PackageVersion,
                x.Status,
                x.ConfigurationJson,
                x.LastStartedAtUtc,
                x.LastStoppedAtUtc,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                Runs = db.AgentJobs.Count(r => r.TenantId == TenantId && r.ContainerId == x.Id),
                ActiveRuns = db.AgentJobs.Count(r => r.TenantId == TenantId && r.ContainerId == x.Id &&
                    (r.Status == AgentJobStatus.Queued || r.Status == AgentJobStatus.Running ||
                     r.Status == AgentJobStatus.Waiting || r.Status == AgentJobStatus.Waiting))
            })
            .ToListAsync(ct);

        return Ok(rows.Select(x => new
        {
            x.Id, x.CampaignId, x.AgentId, x.Name, x.PackageCode, x.PackageVersion, x.Status,
            x.ConfigurationJson, x.LastStartedAtUtc, x.LastStoppedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc,
            targetListId = ReadTargetListId(x.ConfigurationJson),
            x.Runs, x.ActiveRuns
        }));
    }

    [HttpPost("campaigns/{id:guid}/containers")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateContainer(Guid id, CampaignContainerCreateRequest input, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();

        var sourceAgent = campaign.AgentId.HasValue
            ? await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
                x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value, ct)
            : null;

        var agent = new AutonomousAcquisitionAgent
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            Name = string.IsNullOrWhiteSpace(input.Name) ? $"{campaign.Name} Container Agent" : $"{input.Name.Trim()} Agent",
            TemplateCode = sourceAgent?.TemplateCode ?? "custom",
            Industry = sourceAgent?.Industry ?? string.Empty,
            Region = sourceAgent?.Region ?? "Europe",
            CountriesJson = sourceAgent?.CountriesJson ?? "[]",
            IcpJson = sourceAgent?.IcpJson ?? "{}",
            MinimumScore = sourceAgent?.MinimumScore ?? 70,
            DailyDiscoveryLimit = sourceAgent?.DailyDiscoveryLimit ?? 50,
            DailyEmailLimit = sourceAgent?.DailyEmailLimit ?? 10,
            RunTimeUtc = sourceAgent?.RunTimeUtc ?? new TimeOnly(8, 0),
            Status = AutonomousAgentStatus.Draft,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        var container = new CampaignContainer
        {
            Id = Guid.NewGuid(),
            TenantId = TenantId,
            CampaignId = campaign.Id,
            AgentId = agent.Id,
            Name = string.IsNullOrWhiteSpace(input.Name) ? $"{campaign.Name} Container" : input.Name.Trim(),
            PackageCode = string.IsNullOrWhiteSpace(input.PackageCode) ? campaign.PackageCode : input.PackageCode.Trim(),
            PackageVersion = string.IsNullOrWhiteSpace(input.PackageVersion) ? campaign.PackageVersion : input.PackageVersion.Trim(),
            ConfigurationJson = BuildContainerConfiguration(input.ConfigurationJson, campaign.TargetListId),
            Status = CampaignContainerStatus.Pending
        };

        db.AutonomousAcquisitionAgents.Add(agent);
        db.CampaignContainers.Add(container);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Containers), new { id = campaign.Id }, new
        {
            container.Id,
            container.CampaignId,
            container.AgentId,
            container.Name,
            container.PackageCode,
            container.PackageVersion,
            container.Status
        });
    }

    [HttpPut("campaigns/{campaignId:guid}/containers/{containerId:guid}/target-list")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SetContainerTargetList(Guid campaignId, Guid containerId, ContainerTargetListRequest input, CancellationToken ct)
    {
        var container = await db.CampaignContainers.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.CampaignId == campaignId && x.Id == containerId, ct);
        if (container is null) return NotFound();

        if (input.TargetListId.HasValue && input.TargetListId.Value != Guid.Empty &&
            !await db.TargetLists.AnyAsync(x => x.TenantId == TenantId && x.Id == input.TargetListId.Value, ct))
            return NotFound(new { code = "target_list_not_found", detail = "The selected prospect group does not exist in this workspace." });

        container.ConfigurationJson = BuildContainerConfiguration(container.ConfigurationJson, input.TargetListId);
        container.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var targetList = input.TargetListId.HasValue
            ? await db.TargetLists.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.Id == input.TargetListId.Value)
                .Select(x => new { x.Id, x.Name, x.Description, x.IcpProfileId, x.Dynamic })
                .SingleOrDefaultAsync(ct)
            : null;

        return Ok(new { container.Id, container.CampaignId, targetList, targetListId = input.TargetListId });
    }

    [HttpPost("campaigns/{campaignId:guid}/containers/{containerId:guid}/start")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> StartContainer(Guid campaignId, Guid containerId, CancellationToken ct)
    {
        var container = await db.CampaignContainers.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.CampaignId == campaignId && x.Id == containerId, ct);
        if (container is null) return NotFound();

        var campaign = await db.Campaigns.FirstAsync(x => x.TenantId == TenantId && x.Id == campaignId, ct);
        if (campaign.Status is CampaignStatus.Completed or CampaignStatus.Stopped)
            return Conflict(new { code = "campaign_not_restartable", detail = $"Campaign is {campaign.Status} and cannot start a container." });

        if (campaign.Status != CampaignStatus.Running)
            campaign.Start();

        var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == container.AgentId, ct);
        if (agent is null) return NotFound();

        container.Status = CampaignContainerStatus.Running;
        container.LastStartedAtUtc = DateTime.UtcNow;
        container.LastStoppedAtUtc = null;
        container.UpdatedAtUtc = DateTime.UtcNow;
        agent.Status = AutonomousAgentStatus.Active;
        agent.UpdatedAtUtc = DateTime.UtcNow;

        var job = await jobFactory.QueueCampaignAsync(
            TenantId,
            campaignId,
            agent.Id,
            container.Id,
            "container.execute",
            $"campaign:{campaignId}:container:{container.Id}",
            true,
            ct);

        db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId,
            Action = "container.started",
            EntityType = "CampaignContainerActivity",
            EntityId = container.Id.ToString(),
            DataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                level = "info",
                jobId = job.Id,
                message = $"Container '{container.Name}' started.",
                data = new { status = job.Status.ToString() }
            })
        });
        await db.SaveChangesAsync(ct);

        return Ok(new { container.Id, container.Status, jobId = job.Id, jobStatus = job.Status.ToString() });
    }

    [HttpPost("campaigns/{campaignId:guid}/containers/{containerId:guid}/stop")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> StopContainer(Guid campaignId, Guid containerId, CancellationToken ct)
    {
        var container = await db.CampaignContainers.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.CampaignId == campaignId && x.Id == containerId, ct);
        if (container is null) return NotFound();

        container.Status = CampaignContainerStatus.Stopped;
        container.LastStoppedAtUtc = DateTime.UtcNow;
        container.UpdatedAtUtc = DateTime.UtcNow;

        var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == container.AgentId, ct);
        if (agent is not null)
        {
            agent.Status = AutonomousAgentStatus.Stopped;
            agent.UpdatedAtUtc = DateTime.UtcNow;
        }

db.AuditLogs.Add(new AuditLog
        {
            TenantId = TenantId,
            Action = "container.stopped",
            EntityType = "CampaignContainerActivity",
            EntityId = container.Id.ToString(),
            DataJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                level = "info",
                message = $"Container '{container.Name}' stopped.",
                data = new { status = "stopped" }
            })
        });

        await db.SaveChangesAsync(ct);
        return Ok(new { container.Id, container.Status });
    }

    [HttpGet("campaigns/{campaignId:guid}/containers/{containerId:guid}/activity")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> ContainerActivity(
        Guid campaignId,
        Guid containerId,
        [FromQuery] Guid? taskId,
        [FromQuery] string? stepType,
        CancellationToken ct)
    {
        var tenantId = TenantId;
        var valid = await db.CampaignContainers.AnyAsync(
            x => x.TenantId == tenantId && x.CampaignId == campaignId && x.Id == containerId, ct);
        if (!valid) return NotFound();

        var rows = await db.AuditLogs.AsNoTracking()
            .Where(x => x.TenantId == tenantId &&
                        x.EntityType == "CampaignContainerActivity" &&
                        x.EntityId == containerId.ToString())
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(500)
            .ToListAsync(ct);

        var items = rows.Select(x =>
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(x.DataJson ?? "{}");
                var root = doc.RootElement;
                Guid? rowTaskId = null;
                if (root.TryGetProperty("taskId", out var taskValue) &&
                    Guid.TryParse(taskValue.GetString(), out var parsedTaskId))
                    rowTaskId = parsedTaskId;

                return new
                {
                    id = x.Id,
                    atUtc = x.CreatedAtUtc,
                    level = root.TryGetProperty("level", out var level) ? level.GetString() ?? "info" : "info",
                    eventType = x.Action,
                    stepId = rowTaskId,
                    stepType = root.TryGetProperty("stepType", out var stepTypeValue) ? stepTypeValue.GetString() ?? "" : "",
                    stepName = root.TryGetProperty("stepName", out var stepNameValue) ? stepNameValue.GetString() ?? "" : "",
                    message = root.TryGetProperty("message", out var message) ? message.GetString() ?? x.Action : x.Action,
                    data = root.TryGetProperty("data", out var data) ? data.Clone() : System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone()
                };
            }
            catch
            {
                return new
                {
                    id = x.Id,
                    atUtc = x.CreatedAtUtc,
                    level = "info",
                    eventType = x.Action,
                    stepId = (Guid?)null,
                    stepType = "",
                    stepName = "",
                    message = x.Action,
                    data = System.Text.Json.JsonDocument.Parse("{}").RootElement.Clone()
                };
            }
        }).Where(x =>
            (!taskId.HasValue || x.stepId == taskId.Value) &&
            (string.IsNullOrWhiteSpace(stepType) ||
             string.Equals(x.stepType, stepType, StringComparison.OrdinalIgnoreCase)))
          .ToList();

        return Ok(items);
    }

    [HttpGet("campaigns/{id:guid}/activity")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> CampaignActivity(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId;
        if(!await db.Campaigns.AnyAsync(x => x.TenantId==tenantId&&x.Id==id, ct)) return NotFound();
        var messageRows = await db.OutreachMessages.AsNoTracking().Where(x => x.TenantId==tenantId&&x.CampaignId==id)
            .OrderByDescending(x => x.UpdatedAtUtc).Take(100)
            .Select(x => new { x.Id, x.UpdatedAtUtc, x.Status, x.Subject, x.ProviderMessageId }).ToListAsync(ct);
        var messages = messageRows.Select(x => new CampaignActivityItem(x.Id, x.UpdatedAtUtc, "message", x.Status.ToString(), x.Subject, x.ProviderMessageId));
        var replies = await db.ProspectReplies.AsNoTracking().Where(x => x.TenantId==tenantId&&x.CampaignId==id)
            .OrderByDescending(x => x.ReceivedAtUtc).Take(100)
            .Select(x => new CampaignActivityItem(x.Id, x.ReceivedAtUtc, "reply", x.Classification, "Prospect reply", x.Body)).ToListAsync(ct);
        return Ok(messages.Concat(replies).OrderByDescending(x => x.AtUtc).Take(100));
    }

    [HttpGet("messages")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Messages([FromQuery] OutreachStatus? status, CancellationToken ct)
    {
        var tenantId = TenantId;
        var query = from message in db.OutreachMessages.AsNoTracking()
                    join prospect in db.Prospects.AsNoTracking() on message.ProspectId equals prospect.Id
                    join campaign in db.Campaigns.AsNoTracking() on message.CampaignId equals campaign.Id
                    where message.TenantId==tenantId&&(!status.HasValue||message.Status==status.Value)
                    orderby message.CreatedAtUtc descending
                    select new
                    {
                        message.Id,
                        message.CampaignId,
                        campaign = campaign.Name,
                        message.ProspectId,
                        prospect = prospect.CompanyName,
                        prospect.ContactName,
                        prospect.Email,
                        message.Subject,
                        message.Body,
                        message.Status,
                        message.ProviderMessageId,
                        message.SentAtUtc,
                        message.CreatedAtUtc,
                        approvalRequested = db.CrmTasks.Any(task => task.TenantId==tenantId&&task.Title=="APPROVAL: Send outreach "+message.Id&&!task.Completed)
                    };
        return Ok(await query.Take(200).ToListAsync(ct));
    }

    [HttpPost("campaigns/{id:guid}/start")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        Guid? runId = null;
        var strategy = db.Database.CreateExecutionStrategy();

        try
        {
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

                var campaign = await db.Campaigns
                    .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
                if (campaign is null)
                    throw new KeyNotFoundException($"Campaign '{id}' was not found.");

                campaign.Start();
                var agentId = campaign.AgentId;
                if (!agentId.HasValue)
                    throw new InvalidOperationException("Campaign agent is not configured.");

                var job = await jobFactory.QueueCampaignAsync(
                    TenantId,
                    campaign.Id,
                    agentId.Value,
                    null,
                    "campaign.execute",
                    $"campaign:{campaign.Id}",
                    true,
                    ct);
                runId = job.Id;

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }

        return Ok(new
        {
            id,
            status = CampaignStatus.Running,
            execution = "campaign-runtime",
            jobId = runId
        });
    }

    [HttpDelete("campaigns/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tenantId = TenantId;
        var campaign = await db.Campaigns
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);

        if (campaign is null) return NotFound();

        var targetListId = campaign.TargetListId;
        var agentId = campaign.AgentId;

        var runIds = await db.AgentJobs
            .Where(x => x.TenantId == tenantId && x.CampaignId == id)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var messageIds = await db.OutreachMessages
            .Where(x => x.TenantId == tenantId && x.CampaignId == id)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var approvalTitles = messageIds
            .Select(messageId => $"APPROVAL: Send outreach {messageId}")
            .ToList();

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            if (approvalTitles.Count > 0)
                await db.CrmTasks
                    .Where(x => x.TenantId == tenantId && approvalTitles.Contains(x.Title))
                    .ExecuteDeleteAsync(ct);

            if (messageIds.Count > 0)
                await db.OutreachMessages
                    .Where(x => x.TenantId == tenantId && messageIds.Contains(x.Id))
                    .ExecuteDeleteAsync(ct);

            if (runIds.Count > 0)
                await db.AutonomousAcquisitionTasks
                    .Where(x => x.TenantId == tenantId && x.RunId.HasValue && runIds.Contains(x.RunId.Value))
                    .ExecuteDeleteAsync(ct);

            if (runIds.Count > 0)
                await db.AgentJobs
                    .Where(x => x.TenantId == tenantId && runIds.Contains(x.Id))
                    .ExecuteDeleteAsync(ct);

            await db.CampaignRecipients
                .Where(x => x.TenantId == tenantId && x.CampaignId == id)
                .ExecuteDeleteAsync(ct);

            await db.CampaignSteps
                .Where(x => x.TenantId == tenantId && x.CampaignId == id)
                .ExecuteDeleteAsync(ct);

            await db.TargetListMembers
                .Where(x => x.TenantId == tenantId && x.TargetListId == targetListId)
                .ExecuteDeleteAsync(ct);

            await db.Campaigns
                .Where(x => x.TenantId == tenantId && x.Id == id)
                .ExecuteDeleteAsync(ct);

            if (agentId.HasValue)
                await db.AutonomousAcquisitionAgents
                    .Where(x => x.TenantId == tenantId && x.Id == agentId.Value)
                    .ExecuteDeleteAsync(ct);

            await db.TargetLists
                .Where(x => x.TenantId == tenantId && x.Id == targetListId)
                .ExecuteDeleteAsync(ct);

            await transaction.CommitAsync(ct);
        });

        return NoContent();
    }

    [HttpPost("campaigns/{id:guid}/stop")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Stop(Guid id, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();

        campaign.Status = CampaignStatus.Stopped;

        if (campaign.AgentId.HasValue)
        {
            var agent = await db.AutonomousAcquisitionAgents
                .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value, ct);
            if (agent is not null)
            {
                agent.Status = AutonomousAgentStatus.Stopped;
                agent.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

await db.SaveChangesAsync(ct);
        return Ok(new { campaign.Id, campaign.Status });
    }

    [HttpPost("messages/{id:guid}/delivered")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Delivered(Guid id, DeliveryConfirmation input, CancellationToken ct) =>
        await executor.ConfirmDeliveryAsync(TenantId, id, input.ProviderMessageId, ct) ? Ok() : NotFound();

    [HttpPost("messages/{id:guid}/reject-approval")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> RejectApproval(Guid id, CancellationToken ct)
    {
        var message = await db.OutreachMessages.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == id, ct);
        if (message is null) return NotFound();

        var task = await db.CrmTasks.FirstOrDefaultAsync(
            x => x.TenantId == TenantId &&
                 x.Title == $"APPROVAL: Send outreach {id}" &&
                 !x.Completed, ct);
        if (task is null)
            return BadRequest(new { detail = "Request approval before rejecting this message." });

        task.Completed = true;
        message.Status = OutreachStatus.Suppressed;
        await db.SaveChangesAsync(ct);
        return Ok(new { message.Id, rejected = true, status = message.Status });
    }

    [HttpPost("replies")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Reply(ReplyInput input, CancellationToken ct)
    {
        try
        {
            var result = await replyProcessor.ProcessAsync(TenantId, new ProcessProspectReplyRequest(
                input.CampaignId, input.ProspectId, input.OutreachMessageId, input.Body,
                input.Classification, input.SentimentScore, input.RequiresHuman), ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch(InvalidOperationException exception)
        {
            return BadRequest(new { detail = exception.Message });
        }
    }


    private static Guid? ReadTargetListId(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson)) return null;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(configurationJson);
            if (document.RootElement.TryGetProperty("targetListId", out var value) &&
                value.ValueKind == System.Text.Json.JsonValueKind.String &&
                Guid.TryParse(value.GetString(), out var id))
                return id;
        }
        catch (System.Text.Json.JsonException) { }
        return null;
    }

    private static string BuildContainerConfiguration(string? configurationJson, Guid? targetListId)
    {
        var data = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(configurationJson))
        {
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(configurationJson);
                if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
                    foreach (var property in document.RootElement.EnumerateObject())
                        data[property.Name] = property.Value.Clone();
            }
            catch (System.Text.Json.JsonException) { }
        }

        if (targetListId.HasValue) data["targetListId"] = targetListId.Value;
        else data.Remove("targetListId");
        return System.Text.Json.JsonSerializer.Serialize(data);
    }

    private static int ReadMinimumScore(string? criteriaJson)
    {
        if (string.IsNullOrWhiteSpace(criteriaJson)) return 70;
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(criteriaJson);
            if (document.RootElement.TryGetProperty("minimumScore", out var value) && value.TryGetInt32(out var score))
                return Math.Clamp(score, 0, 100);
        }
        catch (System.Text.Json.JsonException) { }
        return 70;
    }

    private static string NormalizeCriteria(string? criteriaJson, int minimumScore)
    {
        var score = Math.Clamp(minimumScore, 0, 100);
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(string.IsNullOrWhiteSpace(criteriaJson) ? "{}" : criteriaJson);
            if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                var map = new Dictionary<string, object?>();
                foreach (var property in document.RootElement.EnumerateObject())
                    map[property.Name] = property.Value.Clone();
                map["minimumScore"] = score;
                return System.Text.Json.JsonSerializer.Serialize(map);
            }
        }
        catch (System.Text.Json.JsonException) { }
        return System.Text.Json.JsonSerializer.Serialize(new { minimumScore = score });
    }

    private static string NormalizeDomain(string? value)
    {
        var domain = (value??string.Empty).Trim().ToLowerInvariant();
        domain=domain.Replace("https://", string.Empty).Replace("http://", string.Empty);
        if(domain.StartsWith("www.")) domain=domain[4..];
        return domain.Split('/')[0].TrimEnd('.');
    }




}

public sealed record DiscoveryRequest(
    string? Source = null,
    string? Region = null,
    int MaximumResults = 50,
    int MinimumScore = 70,
    string? TargetListName = null,
    bool CreateTargetList = true);

public sealed record IcpSaveRequest(
    Guid? Id,
    string Name,
    string? Industry,
    string? CountriesCsv,
    int? MinimumEmployees,
    int? MaximumEmployees,
    string? IntentKeywordsCsv,
    string? CriteriaJson,
    bool Active = true,
    int MinimumScore = 70);

public sealed record CampaignContainerCreateRequest(string? Name, string? PackageCode, string? PackageVersion, string? ConfigurationJson);
public sealed record ContainerTargetListRequest(Guid? TargetListId);
public sealed record CampaignPlanRequest(string PlanJson);
public sealed record CampaignMessagesRequest(IReadOnlyList<CampaignMessageStepRequest> Steps);
public sealed record CampaignMessageStepRequest(int StepNumber, int DelayHours, string Channel, string SubjectTemplate, string BodyTemplate);
public sealed record DeliveryConfirmation(string ProviderMessageId);
public sealed record ReplyInput(Guid TenantId, Guid CampaignId, Guid ProspectId, Guid? OutreachMessageId, string Body, string Classification, int SentimentScore, bool RequiresHuman);
public sealed record CampaignActivityItem(Guid Id, DateTime AtUtc, string Type, string Status, string Title, string Detail);
