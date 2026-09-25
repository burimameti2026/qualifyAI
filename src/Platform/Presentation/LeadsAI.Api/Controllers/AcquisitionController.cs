using System.Text.Json;
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
                sent = db.OutreachMessages.Count(x => x.TenantId==tenantId&&x.CampaignId==campaign.Id&&(x.Status==OutreachStatus.Sent||x.Status==OutreachStatus.Delivered||x.Status==OutreachStatus.Replied))
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
            orderby prospect.PriorityScore descending, prospect.CompanyName
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
                    x.ResultJson,
                    x.Error
                })
                .ToListAsync(ct)).Cast<object>().ToList();

        return Ok(new { campaign, steps, icp, targetList, prospects, latestRun, tasks });
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
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();
        try
        {
            campaign.Status = CampaignStatus.Running;
            if (campaign.AgentId.HasValue)
            {
                var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
                    x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value, ct);
                if (agent is not null)
                {
                    agent.Status = AutonomousAgentStatus.Active;
                    agent.UpdatedAtUtc = DateTime.UtcNow;
                }
            }
            await db.SaveChangesAsync(ct);
            return Ok(new { campaign.Id, campaign.Status });
        }
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
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct);
        if (campaign is null) return NotFound();

        campaign.Start();

        if (campaign.AgentId.HasValue)
        {
            var agent = await db.AutonomousAcquisitionAgents
                .FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == campaign.AgentId.Value, ct);
            if (agent is not null)
            {
                agent.Status = AutonomousAgentStatus.Active;
                agent.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { campaign.Id, campaign.Status, execution = "campaign-runtime" });
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

    private static string NormalizeDomain(string? value)
    {
        var domain = (value??string.Empty).Trim().ToLowerInvariant();
        domain=domain.Replace("https://", string.Empty).Replace("http://", string.Empty);
        if(domain.StartsWith("www.")) domain=domain[4..];
        return domain.Split('/')[0].TrimEnd('.');
    }




}

