using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule("golden_pipeline")]
[Route("api/golden-pipeline")]
public sealed class GoldenPipelineController(ITenantContext tenant, AppDbContext db, IGoldenPipelineProvisioner provisioner) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var pipeline = await provisioner.EnsureProvisionedAsync(tenantId, ct);
        var stages = await db.PipelineStages.Where(x => x.TenantId == tenantId && x.PipelineId == pipeline.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);
        var opportunities = await db.Opportunitys.Where(x => x.TenantId == tenantId).OrderByDescending(x => x.Amount).ToListAsync(ct);
        return Ok(new { pipeline = new { pipeline.Id, pipeline.Name, pipeline.IsDefault }, stages = stages.Select(stage => new { stage.Id, stage.Name, stage.SortOrder, stage.Probability, opportunities = opportunities.Where(o => o.PipelineStageId == stage.Id).Select(o => new { o.Id, o.Name, o.Amount, o.Status, o.ExpectedCloseUtc, o.CompanyId, o.ContactId }).ToArray() }) });
    }

    [HttpGet("stages")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Stages(CancellationToken ct)
    {
        var pipeline = await provisioner.EnsureProvisionedAsync(tenant.TenantId(), ct);
        var stages = await db.PipelineStages.Where(x => x.TenantId == tenant.TenantId() && x.PipelineId == pipeline.Id).OrderBy(x => x.SortOrder).ToListAsync(ct);
        return Ok(stages);
    }

    [HttpPost("provision")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Provision(CancellationToken ct)
    {
        var pipeline = await provisioner.EnsureProvisionedAsync(tenant.TenantId(), ct);
        return Ok(new { pipeline.Id, pipeline.Name, pipeline.IsDefault });
    }

    [HttpPut("opportunities/{id:guid}/stage")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Move(Guid id, GoldenPipelineMoveRequest request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var opportunity = await db.Opportunitys.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (opportunity is null) return NotFound();
        var stage = await db.PipelineStages.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == request.StageId, ct);
        if (stage is null) return BadRequest(new { error = "Invalid pipeline stage." });
        opportunity.PipelineStageId = stage.Id;
        await db.SaveChangesAsync(ct);
        return Ok(new { opportunity.Id, opportunity.PipelineStageId });
    }
}

public sealed record GoldenPipelineMoveRequest(Guid StageId);
