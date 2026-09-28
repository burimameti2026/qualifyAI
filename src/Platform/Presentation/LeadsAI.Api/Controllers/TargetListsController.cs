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
        var rows = await db.TargetLists
            .AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
        return Ok(rows);
    }
}
