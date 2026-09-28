using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Commands.Modules;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Infrastructure.Automation;
using LeadsAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;
using MassTransit;
using LeadsAI.Automation.Application.IntegrationEvents;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Automation)]
[Route("api/workflows")]
public sealed class WorkflowsController(ISender sender, ITenantContext tenant, AppDbContext db, AutomationActionExecutor executor, IAgentJobFactory jobFactory, ICampaignContainerRuntime containers) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<IReadOnlyList<QualificationFlow>> List(CancellationToken ct) => sender.Send(new ListWorkflowsQuery(tenant.TenantId()), ct);

    [HttpGet("{id:guid}/designer")]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public Task<WorkflowDesignerDto> Designer(Guid id, CancellationToken ct) => sender.Send(new GetWorkflowDesignerQuery(tenant.TenantId(), id), ct);

    [HttpPut("{id:guid}/designer")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public Task<WorkflowSaveResult> Save(Guid id, WorkflowDesignerInput input, CancellationToken ct)
        => sender.Send(new SaveWorkflowDesignerCommand(tenant.TenantId(), id, input.Nodes, input.Edges), ct);

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> Create([FromBody] CreateWorkflowRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { code = "workflow_name_required", detail = "Workflow name is required." });

        var flow = new QualificationFlow
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.TenantId(),
            Name = input.Name.Trim(),
            Active = input.Active,
            Trigger = string.IsNullOrWhiteSpace(input.Trigger) ? "manual" : input.Trigger.Trim().ToLowerInvariant(),
            AutomationRuleIdsJson = "[]",
            ContainerIdsJson = "[]"
        };

        db.QualificationFlows.Add(flow);
        await db.SaveChangesAsync(ct);
        return Created($"/api/workflows/{flow.Id}", flow);
    }

    [HttpPost("{id:guid}/run")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> RunOrchestration(Guid id, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var flow = await db.QualificationFlows.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (flow is null) return NotFound();
        if (!flow.Active) return Conflict(new { code = "workflow_inactive", detail = "The workflow is inactive and cannot be executed." });

        var containerIds = ReadIds(flow.ContainerIdsJson);
        var campaignContainers = await db.CampaignContainers
            .Where(x => x.TenantId == tenantId && containerIds.Contains(x.Id))
            .ToListAsync(ct);

        var started = new List<object>();
        foreach (var container in campaignContainers)
        {
            if (container.Status == CampaignContainerStatus.Running) continue;
            var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == container.CampaignId, ct);
            if (campaign is null) continue;
            if (campaign.Status is CampaignStatus.Completed or CampaignStatus.Stopped)
                return Conflict(new { code = "campaign_not_restartable", detail = $"Campaign '{campaign.Name}' is {campaign.Status} and cannot start container '{container.Name}'." });

            var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == container.AgentId, ct);
            if (agent is null) continue;
            containers.Queue(container);
            agent.Status = AutonomousAgentStatus.Active;
            agent.UpdatedAtUtc = DateTime.UtcNow;
            var job = await jobFactory.QueueCampaignAsync(
                tenantId,
                campaign.Id,
                agent.Id,
                container.Id,
                "container.execute",
                $"campaign:{campaign.Id}:container:{container.Id}",
                true,
                ct);
            started.Add(new { containerId = container.Id, container = container.Name, runId = job.Id, jobId = job.Id });
        }
        await db.SaveChangesAsync(ct);

        var automationIds = ReadIds(flow.AutomationRuleIdsJson);
        var automationResults = new List<object>();
        var rules = await db.AutomationRules.Where(x => x.TenantId == tenantId && automationIds.Contains(x.Id) && x.Active).ToListAsync(ct);
        foreach (var rule in rules)
        {
            var run = AutomationRun.Create(tenantId, rule.Id, System.Text.Json.JsonSerializer.Serialize(new { workflowId = flow.Id, campaignId = flow.CampaignId }));
            db.AutomationRuns.Add(run);
            run.Start();
            var result = await executor.ExecuteAsync(rule, run, ct);
            if (result.Success) run.Complete(result.LogJson); else run.Fail(result.LogJson);
            automationResults.Add(new { ruleId = rule.Id, rule = rule.Name, runId = run.Id, success = result.Success, error = result.Error });
        }
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            workflowId = flow.Id,
            campaignId = flow.CampaignId,
            pipelineId = flow.PipelineId,
            startedContainers = started,
            automations = automationResults,
            execution = "workflow-orchestration"
        });
    }

    [HttpGet("{id:guid}/orchestration")]
    [RequirePermission(QualifyAiPermissions.AutomationRead)]
    public async Task<IActionResult> Orchestration(Guid id, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var flow = await db.QualificationFlows.AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (flow is null) return NotFound();

        return Ok(await BuildOrchestrationResponse(flow, tenantId, ct));
    }

    [HttpPut("{id:guid}/orchestration")]
    [RequirePermission(QualifyAiPermissions.AutomationManage)]
    public async Task<IActionResult> BindOrchestration(Guid id, [FromBody] WorkflowOrchestrationRequest input, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var flow = await db.QualificationFlows
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (flow is null) return NotFound();

        if (input.CampaignId.HasValue &&
            !await db.Campaigns.AnyAsync(x => x.TenantId == tenantId && x.Id == input.CampaignId.Value, ct))
            return BadRequest(new { code = "campaign_not_found", detail = "The selected campaign does not belong to this tenant." });

        if (input.PipelineId.HasValue &&
            !await db.Pipelines.AnyAsync(x => x.TenantId == tenantId && x.Id == input.PipelineId.Value, ct))
            return BadRequest(new { code = "pipeline_not_found", detail = "The selected pipeline does not belong to this tenant." });

        var automationIds = input.AutomationRuleIds.Distinct().Where(x => x != Guid.Empty).ToArray();
        var validAutomationIds = await db.AutomationRules
            .Where(x => x.TenantId == tenantId && automationIds.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (validAutomationIds.Count != automationIds.Length)
            return BadRequest(new { code = "automation_not_found", detail = "One or more selected automations do not belong to this tenant." });

        var containerIds = input.ContainerIds.Distinct().Where(x => x != Guid.Empty).ToArray();
        var containers = await db.CampaignContainers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && containerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CampaignId })
            .ToListAsync(ct);
        if (containers.Count != containerIds.Length)
            return BadRequest(new { code = "container_not_found", detail = "One or more selected campaign containers do not belong to this tenant." });

        if (containerIds.Length > 0 && !input.CampaignId.HasValue)
            return BadRequest(new { code = "campaign_required_for_containers", detail = "A campaign is required when workflow containers are selected." });

        if (input.CampaignId.HasValue && containers.Any(x => x.CampaignId != input.CampaignId.Value))
            return BadRequest(new { code = "container_campaign_mismatch", detail = "All selected containers must belong to the selected campaign." });

        flow.CampaignId = input.CampaignId;
        flow.PipelineId = input.PipelineId;
        flow.AutomationRuleIdsJson = System.Text.Json.JsonSerializer.Serialize(validAutomationIds);
        flow.ContainerIdsJson = System.Text.Json.JsonSerializer.Serialize(containerIds);
        flow.Trigger = string.IsNullOrWhiteSpace(input.Trigger) ? "manual" : input.Trigger.Trim().ToLowerInvariant();
        flow.Active = input.Active;
        flow.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return Ok(await BuildOrchestrationResponse(flow, tenantId, ct));
    }

    private async Task<object> BuildOrchestrationResponse(QualificationFlow flow, Guid tenantId, CancellationToken ct)
    {
        var automationIds = ReadIds(flow.AutomationRuleIdsJson);
        var containerIds = ReadIds(flow.ContainerIdsJson);

        var campaign = flow.CampaignId.HasValue
            ? await db.Campaigns.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == flow.CampaignId.Value)
                .Select(x => new { x.Id, x.Name, x.Status, x.PackageCode, x.PackageVersion })
                .SingleOrDefaultAsync(ct)
            : null;

        var pipeline = flow.PipelineId.HasValue
            ? await db.Pipelines.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.Id == flow.PipelineId.Value)
                .Select(x => new { x.Id, x.Name, x.IsDefault })
                .SingleOrDefaultAsync(ct)
            : null;

        var automations = await db.AutomationRules.AsNoTracking()
            .Where(x => x.TenantId == tenantId && automationIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name, x.Trigger, x.Active })
            .ToListAsync(ct);

        var containers = await db.CampaignContainers.AsNoTracking()
            .Where(x => x.TenantId == tenantId && containerIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CampaignId, x.Name, x.Status, x.AgentId, x.PackageCode, x.PackageVersion })
            .ToListAsync(ct);

        return new
        {
            flow.Id,
            flow.TenantId,
            flow.Name,
            flow.Active,
            flow.Trigger,
            campaign,
            pipeline,
            automations,
            containers
        };
    }

    private static Guid[] ReadIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<Guid>();
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<Guid[]>(json) ?? Array.Empty<Guid>();
        }
        catch (System.Text.Json.JsonException)
        {
            return Array.Empty<Guid>();
        }
    }
}
