using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
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
    ProspectDiscoveryService discovery) : ControllerBase
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
                runs = db.AutonomousAcquisitionAgentRuns.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id),
                runSuccess = db.AutonomousAcquisitionAgentRuns.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status==AutonomousAgentRunStatus.Completed),
                runFailed = db.AutonomousAcquisitionAgentRuns.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status==AutonomousAgentRunStatus.Failed),
                runPending = db.AutonomousAcquisitionAgentRuns.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&(x.Status==AutonomousAgentRunStatus.Queued||x.Status==AutonomousAgentRunStatus.WaitingApproval)),
                runRunning = db.AutonomousAcquisitionAgentRuns.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&x.Status==AutonomousAgentRunStatus.Running)
            }).ToListAsync(ct);
        return Ok(campaigns);
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

        var latestRun = await db.AutonomousAcquisitionAgentRuns.AsNoTracking()
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

            await db.AutonomousAcquisitionAgentRuns
                .Where(x => x.TenantId == TenantId &&
                            x.CampaignId == id &&
                            x.Status == AutonomousAgentRunStatus.Queued)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, AutonomousAgentRunStatus.Paused)
                    .SetProperty(x => x.CompletedAtUtc, (DateTime?)null), ct);

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
                var run = await QueueCampaignRunAsync(campaign, isManual: true, ct);
                runId = run?.Id;

                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });

            return Ok(new { id, status = CampaignStatus.Running, runId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
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
                var run = await QueueCampaignRunAsync(campaign, isManual: true, ct);
                runId = run?.Id;

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
            runId
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

        var runIds = await db.AutonomousAcquisitionAgentRuns
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
                await db.AutonomousAcquisitionAgentRuns
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

        await db.AutonomousAcquisitionAgentRuns
            .Where(x => x.TenantId == TenantId &&
                        x.CampaignId == id &&
                        (x.Status == AutonomousAgentRunStatus.Queued ||
                         x.Status == AutonomousAgentRunStatus.WaitingApproval ||
                         x.Status == AutonomousAgentRunStatus.Paused))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(x => x.Status, AutonomousAgentRunStatus.Cancelled)
                .SetProperty(x => x.CompletedAtUtc, DateTime.UtcNow), ct);

        await db.SaveChangesAsync(ct);
        return Ok(new { campaign.Id, campaign.Status });
    }

    [HttpPost("messages/{id:guid}/delivered")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Delivered(Guid id, DeliveryConfirmation input, CancellationToken ct) =>
        await executor.ConfirmDeliveryAsync(TenantId, id, input.ProviderMessageId, ct) ? Ok() : NotFound();

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

    private async Task<AutonomousAcquisitionAgentRun?> QueueCampaignRunAsync(
        Campaign campaign,
        bool isManual,
        CancellationToken ct)
    {
        if (!campaign.AgentId.HasValue)
            return null;

        var agent = await db.AutonomousAcquisitionAgents
            .FirstOrDefaultAsync(
                x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value,
                ct);

        if (agent is null)
            return null;

        agent.Status = AutonomousAgentStatus.Active;
        agent.UpdatedAtUtc = DateTime.UtcNow;

        var existing = await db.AutonomousAcquisitionAgentRuns
            .Where(x => x.TenantId == TenantId &&
                        x.CampaignId == campaign.Id &&
                        x.AgentId == agent.Id &&
                        (x.Status == AutonomousAgentRunStatus.Queued ||
                         x.Status == AutonomousAgentRunStatus.Running ||
                         x.Status == AutonomousAgentRunStatus.WaitingApproval))
            .OrderByDescending(x => x.ScheduledAtUtc)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
            return existing;

        var paused = await db.AutonomousAcquisitionAgentRuns
            .Where(x => x.TenantId == TenantId &&
                        x.CampaignId == campaign.Id &&
                        x.AgentId == agent.Id &&
                        x.Status == AutonomousAgentRunStatus.Paused)
            .OrderByDescending(x => x.ScheduledAtUtc)
            .FirstOrDefaultAsync(ct);

        if (paused is not null)
        {
            paused.Status = AutonomousAgentRunStatus.Queued;
            paused.ScheduledAtUtc = DateTime.UtcNow;
            paused.CompletedAtUtc = null;
            paused.Error = null;
            return paused;
        }

        var run = new AutonomousAcquisitionAgentRun
        {
            TenantId = TenantId,
            AgentId = agent.Id,
            CampaignId = campaign.Id,
            IsManual = isManual,
            Status = AutonomousAgentRunStatus.Queued,
            ScheduledAtUtc = DateTime.UtcNow
        };

        db.AutonomousAcquisitionAgentRuns.Add(run);
        return run;
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

public sealed record CampaignPlanRequest(string PlanJson);
public sealed record CampaignMessagesRequest(IReadOnlyList<CampaignMessageStepRequest> Steps);
public sealed record CampaignMessageStepRequest(int StepNumber, int DelayHours, string Channel, string SubjectTemplate, string BodyTemplate);
public sealed record DeliveryConfirmation(string ProviderMessageId);
public sealed record ReplyInput(Guid TenantId, Guid CampaignId, Guid ProspectId, Guid? OutreachMessageId, string Body, string Classification, int SentimentScore, bool RequiresHuman);
public sealed record CampaignActivityItem(Guid Id, DateTime AtUtc, string Type, string Status, string Title, string Detail);
