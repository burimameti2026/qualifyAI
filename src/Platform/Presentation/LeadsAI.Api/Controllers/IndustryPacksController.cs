using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Settings)]
[Route("api/industry-packs")]
public sealed class IndustryPacksController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var installed = await db.TenantIndustryPacks
            .Where(x => x.TenantId == tenantId)
            .ToDictionaryAsync(x => x.IndustryPackId, x => x, ct);

        var packs = await db.IndustryPacks
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return Ok(packs.Select(pack => new
        {
            pack.Id,
            pack.Code,
            pack.Name,
            pack.Description,
            pack.TemplateJson,
            installed = installed.ContainsKey(pack.Id),
            enabled = installed.TryGetValue(pack.Id, out var installation) && installation.Enabled
        }));
    }

    [HttpPost("{id:guid}/install")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public async Task<IActionResult> Install(Guid id, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var pack = await db.IndustryPacks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (pack is null) return NotFound();

        var installation = await db.TenantIndustryPacks
            .SingleOrDefaultAsync(x => x.TenantId == tenantId && x.IndustryPackId == id, ct);

        if (installation is null)
        {
            installation = new TenantIndustryPack
            {
                TenantId = tenantId,
                IndustryPackId = id,
                Enabled = true
            };
            db.TenantIndustryPacks.Add(installation);
        }
        else
        {
            installation.Enabled = true;
            installation.UpdatedAtUtc = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            pack.Id,
            pack.Code,
            pack.Name,
            installed = true,
            enabled = installation.Enabled
        });
    }
}
