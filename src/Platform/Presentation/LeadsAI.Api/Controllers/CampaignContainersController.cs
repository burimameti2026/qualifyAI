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
[Route("api/acquisition/campaigns/{campaignId:guid}/containers")]
public sealed class CampaignContainersController(
    AppDbContext db,
    ITenantContext tenant,
    IAgentJobFactory jobFactory,
    ICampaignContainerRuntime containers,
    CampaignContainerConfigurationService configurationService) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Containers(Guid campaignId, CancellationToken ct)
    {
        var exists = await db.Campaigns.AnyAsync(x => x.TenantId == TenantId && x.Id == campaignId, ct);
        if (!exists) return NotFound();

        var rows = await db.CampaignContainers.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.CampaignId == campaignId)
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
                     r.Status == AgentJobStatus.Waiting))
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

    [HttpPost("")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateContainer(Guid campaignId, CampaignContainerCreateRequest input, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == campaignId, ct);
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
            Status = CampaignContainerStatus.Queued
        };

        // A container created from a campaign becomes the campaign's canonical runtime agent.
        // Without this link, the campaign Run endpoint cannot resolve the newly created container agent.
        campaign.AgentId = agent.Id;
        campaign.UpdatedAtUtc = DateTime.UtcNow;

        db.AutonomousAcquisitionAgents.Add(agent);
        db.CampaignContainers.Add(container);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(Containers), new { campaignId = campaign.Id }, new
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

    [HttpPut("{containerId:guid}/target-list")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SetContainerTargetList(Guid campaignId, Guid containerId, ContainerTargetListRequest input, CancellationToken ct)
    {
        var container = await db.CampaignContainers.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.CampaignId == campaignId && x.Id == containerId, ct);
        if (container is null) return NotFound();

        if (container.Status is CampaignContainerStatus.Running or CampaignContainerStatus.Starting or CampaignContainerStatus.Queued or CampaignContainerStatus.Waiting)
            return Conflict(new { code = "container_active", detail = "Stop or finish the queued/running container before changing its prospect group." });

        Guid? targetListId = input.TargetListId.HasValue && input.TargetListId.Value != Guid.Empty ? input.TargetListId : null;
        if (targetListId.HasValue &&
            !await db.TargetLists.AnyAsync(x => x.TenantId == TenantId && x.Id == targetListId.Value, ct))
            return NotFound(new { code = "target_list_not_found", detail = "The selected prospect group does not exist in this workspace." });

        container.ConfigurationJson = configurationService.Build(container.ConfigurationJson, targetListId);
        container.Version++;
        container.VersionLabel = $"v{container.Version}";
        container.ChangeSummary = "Container prospect group updated";
        container.UpdatedAtUtc = DateTime.UtcNow;

        var changes = new List<System.Text.Json.JsonElement>();
        try
        {
            using var previousChanges = System.Text.Json.JsonDocument.Parse(
                string.IsNullOrWhiteSpace(container.ChangesJson) ? "[]" : container.ChangesJson);
            if (previousChanges.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                changes.AddRange(previousChanges.RootElement.EnumerateArray().Select(x => x.Clone()));
        }
        catch (System.Text.Json.JsonException)
        {
            // Keep the new audit entry even when recovering from malformed legacy history.
        }
        changes.Add(System.Text.Json.JsonSerializer.SerializeToElement(new
        {
            atUtc = DateTime.UtcNow,
            change = "prospect-group-updated",
            version = container.Version,
            targetListId
        }));
        container.ChangesJson = System.Text.Json.JsonSerializer.Serialize(changes);
        await db.SaveChangesAsync(ct);

        var targetList = targetListId.HasValue
            ? await db.TargetLists.AsNoTracking()
                .Where(x => x.TenantId == TenantId && x.Id == targetListId.Value)
                .Select(x => new { x.Id, x.Name, x.Description, x.IcpProfileId, x.Dynamic })
                .SingleOrDefaultAsync(ct)
            : null;

        return Ok(new { container.Id, container.CampaignId, targetList, targetListId });
    }

    [HttpPut("{containerId:guid}/configuration")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SaveContainerConfiguration(
        Guid campaignId,
        Guid containerId,
        ContainerConfigurationRequest input,
        CancellationToken ct)
    {
        var container = await db.CampaignContainers.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.CampaignId == campaignId && x.Id == containerId, ct);
        if (container is null) return NotFound();

        if (container.Status is CampaignContainerStatus.Running or CampaignContainerStatus.Starting or CampaignContainerStatus.Queued or CampaignContainerStatus.Waiting)
            return Conflict(new { code = "container_active", detail = "Stop or finish the queued/running container before changing its workflow configuration." });

        if (string.IsNullOrWhiteSpace(input.ConfigurationJson))
            return BadRequest(new { detail = "Container configuration is required." });

        System.Text.Json.JsonDocument parsed;
        try
        {
            parsed = System.Text.Json.JsonDocument.Parse(input.ConfigurationJson);
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new { detail = "Container configuration must be valid JSON." });
        }

        using (parsed)
        {
            if (parsed.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return BadRequest(new { detail = "Container configuration must be a JSON object." });

            var targetListId = configurationService.ReadTargetListId(input.ConfigurationJson);
            if (targetListId.HasValue &&
                !await db.TargetLists.AnyAsync(x => x.TenantId == TenantId && x.Id == targetListId.Value, ct))
                return NotFound(new { code = "target_list_not_found", detail = "The selected prospect group does not exist in this workspace." });

            // Keep the workflow graph and target-list binding in this container's own
            // versioned configuration; do not overwrite the campaign's shared plan.
            container.ConfigurationJson = input.ConfigurationJson;
            container.Version++;
            container.VersionLabel = $"v{container.Version}";
            container.ChangeSummary = "Container workflow configuration updated";
            container.UpdatedAtUtc = DateTime.UtcNow;
            var changes = new List<System.Text.Json.JsonElement>();
            try
            {
                using var previousChanges = System.Text.Json.JsonDocument.Parse(
                    string.IsNullOrWhiteSpace(container.ChangesJson) ? "[]" : container.ChangesJson);
                if (previousChanges.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    changes.AddRange(previousChanges.RootElement.EnumerateArray().Select(x => x.Clone()));
            }
            catch (System.Text.Json.JsonException)
            {
                // Recover gracefully from a malformed legacy history without losing the new event.
            }
            changes.Add(System.Text.Json.JsonSerializer.SerializeToElement(new
            {
                atUtc = DateTime.UtcNow,
                change = "workflow-configuration-saved",
                version = container.Version
            }));
            container.ChangesJson = System.Text.Json.JsonSerializer.Serialize(changes);
            await db.SaveChangesAsync(ct);
        }

        return Ok(new { container.Id, container.Version, container.VersionLabel, container.ConfigurationJson });
    }

    [HttpPost("{containerId:guid}/start")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> StartContainer(Guid campaignId, Guid containerId, CancellationToken ct)
    {
        var container = await db.CampaignContainers.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.CampaignId == campaignId && x.Id == containerId, ct);
        if (container is null) return NotFound();

        if (container.Status is CampaignContainerStatus.Queued or CampaignContainerStatus.Starting or
            CampaignContainerStatus.Running or CampaignContainerStatus.Waiting)
            return Conflict(new
            {
                code = "container_already_active",
                detail = $"Container is already {container.Status}; wait for the current run or stop it before starting again."
            });

        var activeJob = await db.AgentJobs.AnyAsync(x =>
            x.TenantId == TenantId &&
            x.ContainerId == container.Id &&
            (x.Status == AgentJobStatus.Queued || x.Status == AgentJobStatus.Running ||
             x.Status == AgentJobStatus.Waiting), ct);
        if (activeJob)
            return Conflict(new
            {
                code = "container_job_already_active",
                detail = "An active or waiting job already exists for this container. It will not be queued a second time."
            });

        var campaign = await db.Campaigns.FirstAsync(x => x.TenantId == TenantId && x.Id == campaignId, ct);
        if (campaign.Status is CampaignStatus.Completed or CampaignStatus.Stopped)
            return Conflict(new { code = "campaign_not_restartable", detail = $"Campaign is {campaign.Status} and cannot start a container." });

        var targetListId = configurationService.ReadTargetListId(container.ConfigurationJson)
            ?? (campaign.TargetListId != Guid.Empty ? campaign.TargetListId : (Guid?)null);
        if (!targetListId.HasValue ||
            !await db.TargetLists.AnyAsync(x => x.TenantId == TenantId && x.Id == targetListId.Value, ct))
            return Conflict(new
            {
                code = "container_target_list_required",
                detail = "Select a target list for this container before starting its acquisition workflow."
            });

        var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == container.AgentId, ct);
        if (agent is null) return NotFound();

        if (campaign.Status != CampaignStatus.Running)
            campaign.Start();

        containers.Start(container, DateTime.UtcNow);
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

        db.CampaignContainerActivities.Add(new CampaignContainerActivityRecord
        {
            TenantId = TenantId,
            ContainerId = container.Id,
            RunId = job.Id,
            ContainerVersion = container.Version,
            AtUtc = DateTime.UtcNow,
            Level = "info",
            EventType = "container.started",
            StepType = string.Empty,
            StepName = string.Empty,
            Message = $"Container '{container.Name}' started.",
            DataJson = System.Text.Json.JsonSerializer.Serialize(new { status = job.Status.ToString() })
        });
        await db.SaveChangesAsync(ct);

        return Ok(new { container.Id, container.Status, jobId = job.Id, jobStatus = job.Status.ToString() });
    }

    [HttpPost("{containerId:guid}/stop")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> StopContainer(Guid campaignId, Guid containerId, CancellationToken ct)
    {
        var container = await db.CampaignContainers.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.CampaignId == campaignId && x.Id == containerId, ct);
        if (container is null) return NotFound();

        containers.Stop(container, DateTime.UtcNow);

        var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
            x => x.TenantId == TenantId && x.Id == container.AgentId, ct);
        if (agent is not null)
        {
            agent.Status = AutonomousAgentStatus.Stopped;
            agent.UpdatedAtUtc = DateTime.UtcNow;
        }

db.CampaignContainerActivities.Add(new CampaignContainerActivityRecord
        {
            TenantId = TenantId,
            ContainerId = container.Id,
            ContainerVersion = container.Version,
            AtUtc = DateTime.UtcNow,
            Level = "info",
            EventType = "container.stopped",
            StepType = string.Empty,
            StepName = string.Empty,
            Message = $"Container '{container.Name}' stopped.",
            DataJson = System.Text.Json.JsonSerializer.Serialize(new { status = "stopped" })
        });

        await db.SaveChangesAsync(ct);
        return Ok(new { container.Id, container.Status });
    }

    [HttpGet("{containerId:guid}/activity")]
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

        var query = db.CampaignContainerActivities.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ContainerId == containerId);

        if (taskId.HasValue)
            query = query.Where(x => x.TaskId == taskId.Value);
        if (!string.IsNullOrWhiteSpace(stepType))
            query = query.Where(x => x.StepType == stepType);

        var rows = await query
            .OrderByDescending(x => x.AtUtc)
            .Take(500)
            .ToListAsync(ct);

        var items = rows.Select(x =>
        {
            System.Text.Json.JsonElement data;
            try
            {
                using var document = System.Text.Json.JsonDocument.Parse(
                    string.IsNullOrWhiteSpace(x.DataJson) ? "{}" : x.DataJson);
                data = document.RootElement.Clone();
            }
            catch
            {
                using var document = System.Text.Json.JsonDocument.Parse("{}");
                data = document.RootElement.Clone();
            }

            return new
            {
                id = x.Id,
                atUtc = x.AtUtc,
                level = x.Level,
                eventType = x.EventType,
                runId = x.RunId,
                stepId = x.TaskId,
                stepType = x.StepType,
                stepName = x.StepName,
                message = x.Message,
                data
            };
        });

        return Ok(items);
    }
}

public sealed record ContainerConfigurationRequest(string ConfigurationJson);
