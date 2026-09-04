using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Commands;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Modules;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/sales")]
public sealed class SalesController(ISender sender, ITenantContext tenant, SalesAutomationService sales, AppDbContext db) : ControllerBase
{
    [HttpGet("pipelines")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<SalesPipelinesDto> Pipelines(CancellationToken ct) => sender.Send(new GetSalesPipelinesQuery(tenant.TenantId()), ct);

    [HttpPost("pipelines")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreatePipeline(SalesPipelineInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { detail = "Pipeline name is required." });
        var tenantId = tenant.TenantId();
        // A tenant must always have one deterministic route for automated opportunities.
        // The first pipeline becomes that route; later pipelines are opt-in defaults.
        var isDefault = input.IsDefault || !await db.Pipelines.AnyAsync(x => x.TenantId == tenantId, ct);
        if (isDefault)
            await db.Pipelines.Where(x => x.TenantId == tenantId).ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false), ct);

        var pipeline = new Pipeline { TenantId = tenantId, Name = input.Name.Trim(), IsDefault = isDefault };
        db.Pipelines.Add(pipeline);
        await db.SaveChangesAsync(ct);
        return Created($"/api/sales/pipelines/{pipeline.Id}", pipeline);
    }

    [HttpPut("pipelines/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdatePipeline(Guid id, SalesPipelineInput input, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var pipeline = await db.Pipelines.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (pipeline is null) return NotFound();
        try { pipeline.Rename(input.Name); }
        catch (InvalidOperationException ex) { return BadRequest(new { detail = ex.Message }); }
        if (input.IsDefault)
            await db.Pipelines.Where(x => x.TenantId == tenantId && x.Id != id).ExecuteUpdateAsync(x => x.SetProperty(p => p.IsDefault, false), ct);
        pipeline.IsDefault = input.IsDefault;
        await db.SaveChangesAsync(ct);
        return Ok(pipeline);
    }

    [HttpPost("pipelines/{pipelineId:guid}/stages")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateStage(Guid pipelineId, SalesPipelineStageInput input, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        if (!await db.Pipelines.AnyAsync(x => x.TenantId == tenantId && x.Id == pipelineId, ct)) return NotFound();
        var stage = new PipelineStage { TenantId = tenantId, PipelineId = pipelineId };
        try { stage.Configure(input.Name, input.SortOrder, input.Probability); }
        catch (InvalidOperationException ex) { return BadRequest(new { detail = ex.Message }); }
        db.PipelineStages.Add(stage);
        await db.SaveChangesAsync(ct);
        return Created($"/api/sales/pipelines/{pipelineId}/stages/{stage.Id}", stage);
    }

    [HttpPut("pipelines/{pipelineId:guid}/stages/{stageId:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdateStage(Guid pipelineId, Guid stageId, SalesPipelineStageInput input, CancellationToken ct)
    {
        var stage = await db.PipelineStages.FirstOrDefaultAsync(x => x.TenantId == tenant.TenantId() && x.PipelineId == pipelineId && x.Id == stageId, ct);
        if (stage is null) return NotFound();
        try { stage.Configure(input.Name, input.SortOrder, input.Probability); }
        catch (InvalidOperationException ex) { return BadRequest(new { detail = ex.Message }); }
        await db.SaveChangesAsync(ct);
        return Ok(stage);
    }

    [HttpDelete("pipelines/{pipelineId:guid}/stages/{stageId:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> DeleteStage(Guid pipelineId, Guid stageId, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var stage = await db.PipelineStages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.PipelineId == pipelineId && x.Id == stageId, ct);
        if (stage is null) return NotFound();
        if (await db.Opportunitys.AnyAsync(x => x.TenantId == tenantId && x.PipelineStageId == stageId, ct))
            return Conflict(new { detail = "Move opportunities out of this stage before deleting it." });
        db.PipelineStages.Remove(stage);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("automation/run")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> RunAutomation(CancellationToken ct) => Ok(await sales.RunAsync(tenant.TenantId(), ct));

    [HttpPost("leads/{id:guid}/convert")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Convert(Guid id, CancellationToken ct)
        => (await sales.ConvertLeadAsync(tenant.TenantId(), id, ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("leads/{id:guid}/qualify")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Qualify(Guid id, CancellationToken ct)
        => (await sender.Send(new QualifyLeadCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();

    [HttpGet("tasks")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<CrmTask>> Tasks(CancellationToken ct) => sender.Send(new ListSalesTasksQuery(tenant.TenantId()), ct);

    [HttpPut("tasks/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdateTask(Guid id, CrmTask input, CancellationToken ct)
        => (await sender.Send(new UpdateSalesTaskCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();
}

public sealed record SalesPipelineInput(string Name, bool IsDefault);
public sealed record SalesPipelineStageInput(string Name, int SortOrder, decimal Probability);

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Ai)]
[RequirePermission(QualifyAiPermissions.AgentsRead)]
[Route("api/evaluations")]
public sealed class EvaluationsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("datasets")]
    public Task<IReadOnlyList<EvaluationDataset>> Datasets(CancellationToken ct) => sender.Send(new ListEvaluationDatasetsQuery(tenant.TenantId()), ct);
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Analytics)]
[RequirePermission(QualifyAiPermissions.AnalyticsRead)]
[Route("api/analytics")]
public sealed class AnalyticsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("overview")]
    public Task<AnalyticsOverviewDto> Overview(CancellationToken ct) => sender.Send(new GetAnalyticsOverviewQuery(tenant.TenantId()), ct);
}
