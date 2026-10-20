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
public sealed class IcpController(AppDbContext db, ITenantContext tenant, AcquisitionCriteriaService criteriaService) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("icp")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Icp(CancellationToken ct)
    {
        var rows = await db.IcpProfiles.AsNoTracking()
            .Where(x => x.TenantId == TenantId)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return Ok(rows.Select(x => new
        {
            x.Id,
            x.TenantId,
            x.Name,
            x.Industry,
            x.CountriesCsv,
            x.MinimumEmployees,
            x.MaximumEmployees,
            x.IntentKeywordsCsv,
            x.CriteriaJson,
            x.Active,
            x.LastDiscoveryAtUtc,
            minimumScore = criteriaService.ReadMinimumScore(x.CriteriaJson)
        }));
    }

    [HttpPost("icp")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> SaveIcp([FromBody] IcpSaveRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { error = "ICP name is required." });

        var tenantId = TenantId;
        IcpProfile? profile = null;
        if (input.Id.HasValue && input.Id.Value != Guid.Empty)
            profile = await db.IcpProfiles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == input.Id.Value, ct);

        var isNew = profile is null;
        profile ??= new IcpProfile { Id = Guid.NewGuid(), TenantId = tenantId };

        profile.Name = input.Name.Trim();
        profile.Industry = input.Industry?.Trim() ?? string.Empty;
        profile.CountriesCsv = input.CountriesCsv?.Trim() ?? string.Empty;
        profile.IntentKeywordsCsv = input.IntentKeywordsCsv?.Trim() ?? string.Empty;
        profile.MinimumEmployees = input.MinimumEmployees;
        profile.MaximumEmployees = input.MaximumEmployees;
        profile.CriteriaJson = criteriaService.NormalizeCriteria(input.CriteriaJson, input.MinimumScore);
        profile.Active = input.Active;
        profile.UpdatedAtUtc = DateTime.UtcNow;

        if (isNew)
            db.IcpProfiles.Add(profile);

        await db.SaveChangesAsync(ct);
        return Ok(new
        {
            profile.Id,
            profile.TenantId,
            profile.Name,
            profile.Industry,
            profile.CountriesCsv,
            profile.MinimumEmployees,
            profile.MaximumEmployees,
            profile.IntentKeywordsCsv,
            profile.CriteriaJson,
            profile.Active,
            profile.LastDiscoveryAtUtc,
            minimumScore = criteriaService.ReadMinimumScore(profile.CriteriaJson)
        });
    }
}
