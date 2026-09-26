using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure.IndustryPacks;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
public sealed record ProvisionIndustryPackRequest(string? ScenarioCode);

[Route("api/industry-packs")]
public sealed class IndustryPacksController(
    AppDbContext db,
    ITenantContext tenant,
    IIndustryPackProvisioner provisioner) : ControllerBase
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

        var campaigns = await db.Campaigns
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PackageCode.StartsWith("industry-pack:"))
            .Select(x => new { x.Id, x.PackageCode, x.Status, x.TargetListId, x.Name })
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

        return Ok(packs.Select(pack =>
        {
            var campaign = campaigns.SingleOrDefault(x =>
                x.PackageCode == $"industry-pack:{pack.Code.Trim().ToLowerInvariant()}");

            return new
            {
                pack.Id,
                pack.Code,
                pack.Name,
                pack.Description,
                pack.TemplateJson,
                pack.installed,
                provisioned = campaign is not null,
                campaignId = campaign?.Id,
                targetListId = campaign?.TargetListId,
                campaignName = campaign?.Name,
                campaignStatus = campaign?.Status.ToString()
            };
        }));
    }

    [HttpPost("{id:guid}/install")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public Task<IActionResult> Install(Guid id, CancellationToken ct)
        => Provision(id, null, ct);

    [HttpPost("{id:guid}/provision")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Provision(Guid id, ProvisionIndustryPackRequest? input, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();

        if (!await db.IndustryPacks.AnyAsync(x => x.Id == id, ct))
            return NotFound(new { error = "Industry pack was not found." });

        var result = await provisioner.ProvisionAsync(tenantId, id, ct, input?.ScenarioCode);

        return Ok(new
        {
            provisioned = true,
            result.IndustryPackId,
            result.IndustryCode,
            result.TargetListId,
            result.CampaignId,
            result.CampaignStatus,
            result.ProvisioningMode,
            definition = result.Definition
        });
    }
}
