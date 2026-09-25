using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/industry-packs")]
public sealed class IndustryPacksController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var installed = await db.TenantIndustryPacks
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Enabled)
            .Select(x => x.IndustryPackId)
            .ToListAsync(ct);

        var packs = await db.IndustryPacks
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new
            {
                x.Id,
                x.Code,
                x.Name,
                x.Description,
                x.TemplateJson,
                installed = installed.Contains(x.Id)
            })
            .ToListAsync(ct);

        return Ok(packs);
    }

    [HttpPost("{id:guid}/install")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Install(Guid id, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        if (!await db.IndustryPacks.AnyAsync(x => x.Id == id, ct))
            return NotFound();

        var existing = await db.TenantIndustryPacks
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IndustryPackId == id, ct);

        if (existing is null)
        {
            db.TenantIndustryPacks.Add(new TenantIndustryPack
            {
                TenantId = tenantId,
                IndustryPackId = id,
                Enabled = true
            });
        }
        else
        {
            existing.Enabled = true;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { installed = true, industryPackId = id });
    }
}
