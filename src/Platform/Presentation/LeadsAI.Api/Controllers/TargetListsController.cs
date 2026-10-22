using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Api.Services;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/acquisition")]
public sealed class TargetListsController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("target-lists")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> TargetLists(CancellationToken ct)
    {
        var rows = await db.TargetLists.AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("target-lists")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> CreateTargetList([FromBody] TargetListCreateRequest input, CancellationToken ct)
    {
        var tenantId = TenantId;
        if (string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { detail = "Target list name is required." });
        if (input.IcpProfileId.HasValue &&
            !await db.IcpProfiles.AnyAsync(x => x.TenantId == tenantId && x.Id == input.IcpProfileId.Value, ct))
            return BadRequest(new { detail = "The selected ICP does not belong to this tenant." });

        var list = new TargetList
        {
            TenantId = tenantId,
            Name = input.Name.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            IcpProfileId = input.IcpProfileId,
            Dynamic = input.Dynamic
        };
        db.TargetLists.Add(list);
        await db.SaveChangesAsync(ct);
        return Created($"/api/acquisition/target-lists/{list.Id}", list);
    }

    [HttpPost("target-lists/{id:guid}/members")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddMembers(Guid id, [FromBody] Guid[]? prospectIds, CancellationToken ct)
    {
        var tenantId = TenantId;
        if (!await db.TargetLists.AnyAsync(x => x.TenantId == tenantId && x.Id == id, ct))
            return NotFound(new { detail = "Target list was not found in this tenant." });
        if (prospectIds is null || prospectIds.Length == 0)
            return BadRequest(new { detail = "Select at least one prospect to add." });

        var requested = prospectIds.Distinct().ToArray();
        var valid = await db.Prospects.Where(x => x.TenantId == tenantId && requested.Contains(x.Id))
            .Select(x => x.Id).ToListAsync(ct);
        if (valid.Count != requested.Length)
            return BadRequest(new { detail = "One or more prospects do not belong to this tenant or no longer exist." });

        var existing = await db.TargetListMembers
            .Where(x => x.TenantId == tenantId && x.TargetListId == id)
            .Select(x => x.ProspectId).ToListAsync(ct);
        var additions = valid.Except(existing).ToArray();
        db.TargetListMembers.AddRange(additions.Select(prospectId => new TargetListMember
        {
            TenantId = tenantId,
            TargetListId = id,
            ProspectId = prospectId,
            AddedAtUtc = DateTime.UtcNow
        }));
        await db.SaveChangesAsync(ct);
        return Ok(new { added = additions.Length, alreadyPresent = valid.Count - additions.Length });
    }
}

public sealed record TargetListCreateRequest(string Name, string? Description, Guid? IcpProfileId, bool Dynamic);
