using System.Data;
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
public sealed class CampaignContainersController(
    AppDbContext db,
    ITenantContext tenant,
    IAgentJobFactory jobFactory,
    ICampaignContainerRuntime containers,
    CampaignContainerConfigurationService configurationService) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

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
            targetListId = configurationService.ReadTargetListId(x.ConfigurationJson),
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
            ConfigurationJson = configurationService.Build(input.ConfigurationJson, campaign.TargetListId),
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

        container.ConfigurationJson = configurationService.Build(container.ConfigurationJson, input.TargetListId);
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
}