using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Api.Services;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/acquisition/campaigns")]
public sealed class CampaignsController(
    AppDbContext db,
    ITenantContext tenant,
    CampaignExecutionService executor,
    ProspectReplyProcessingService replyProcessor,
    IAgentJobFactory jobFactory,
    CampaignRuntimeService runtime,
    AcquisitionCriteriaService criteriaService,
    CampaignContainerConfigurationService configurationService) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenantId = TenantId;
        var campaigns = await db.Campaigns.AsNoTracking()
            .Where(x => x.TenantId == tenantId)
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
                recipients = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id),
                active = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "active"),
                awaitingDelivery = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "awaiting-delivery"),
                replied = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "replied"),
                completed = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "completed"),
                failed = db.CampaignRecipients.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == "failed"),
                queued = db.OutreachMessages.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == OutreachStatus.Queued),
                sent = db.OutreachMessages.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && (x.Status == OutreachStatus.Sent || x.Status == OutreachStatus.Delivered || x.Status == OutreachStatus.Replied)),
                jobs = db.AgentJobs.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id),
                completedJobs = db.AgentJobs.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == AgentJobStatus.Completed),
                failedJobs = db.AgentJobs.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == AgentJobStatus.Failed),
                queuedJobs = db.AgentJobs.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && (x.Status == AgentJobStatus.Queued || x.Status == AgentJobStatus.Waiting)),
                runningJobs = db.AgentJobs.Count(x => x.TenantId == tenantId && x.CampaignId == campaign.Id && x.Status == AgentJobStatus.Running)
            })
            .ToListAsync(ct);
        return Ok(campaigns);
    }

    [HttpGet("history")]
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

    [HttpGet("{id:guid}")]
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

    [HttpPut("{id:guid}/plan")]
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

    [HttpPut("{id:guid}/messages")]
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

    [HttpPost("{id:guid}/pause")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Pause(Guid id, CancellationToken ct)
    {
        try
        {
            var status = await runtime.PauseAsync(TenantId, id, ct);
            return status is null ? NotFound() : Ok(new { id, status });
        }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpPost("{id:guid}/resume")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await runtime.ResumeAsync(TenantId, id, ct);
            return result is null ? NotFound() : Ok(new { id, status = result.Status, jobId = result.JobId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpGet("{id:guid}/activity")]
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

    [HttpPost("{id:guid}/start")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Start(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await runtime.StartAsync(TenantId, id, ct);
            return result is null
                ? NotFound()
                : Ok(new { id, status = result.Status, execution = "campaign-runtime", jobId = result.JobId });
        }
        catch (KeyNotFoundException) { return NotFound(); }
        catch (InvalidOperationException ex) { return Conflict(new { detail = ex.Message }); }
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        return await runtime.DeleteAsync(TenantId, id, ct)
            ? NoContent()
            : NotFound();
    }

    [HttpPost("{id:guid}/stop")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Stop(Guid id, CancellationToken ct)
    {
        var status = await runtime.StopAsync(TenantId, id, ct);
        return status is null ? NotFound() : Ok(new { id, status });
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


}
