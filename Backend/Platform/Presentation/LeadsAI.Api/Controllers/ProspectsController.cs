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
public sealed class ProspectsController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

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
}
