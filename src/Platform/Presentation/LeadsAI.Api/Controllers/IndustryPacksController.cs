using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure.IndustryPacks;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api.Controllers;

public sealed record ProvisionIndustryPackRequest(string? ScenarioCode);
public sealed record IndustryPackRequest(string Code, string Name, string? Description, string? TemplateJson);

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
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


    [HttpPost]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Create(IndustryPackRequest input, CancellationToken ct)
    {
        var code = input.Code?.Trim().ToLowerInvariant();
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Code and name are required." });
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z0-9][a-z0-9-]*$"))
            return BadRequest(new { error = "Code may contain lowercase letters, numbers and hyphens only." });
        if (await db.IndustryPacks.AnyAsync(x => x.Code == code, ct))
            return Conflict(new { error = "An Industry Pack with this code already exists." });
        var template = string.IsNullOrWhiteSpace(input.TemplateJson) ? "{}" : input.TemplateJson!;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(template);
            if (json.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return BadRequest(new { error = "TemplateJson must be a JSON object." });
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new { error = "TemplateJson is not valid JSON." });
        }
        var pack = new IndustryPack { Id = Guid.NewGuid(), Code = code, Name = name, Description = input.Description?.Trim() ?? string.Empty, TemplateJson = template };
        db.IndustryPacks.Add(pack);
        await db.SaveChangesAsync(ct);
        return Created($"/api/industry-packs/{pack.Id}", pack);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Update(Guid id, IndustryPackRequest input, CancellationToken ct)
    {
        var pack = await db.IndustryPacks.SingleOrDefaultAsync(x => x.Id == id, ct);
        if (pack is null) return NotFound(new { error = "Industry pack was not found." });
        var code = input.Code?.Trim().ToLowerInvariant();
        var name = input.Name?.Trim();
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            return BadRequest(new { error = "Code and name are required." });
        if (!System.Text.RegularExpressions.Regex.IsMatch(code, "^[a-z0-9][a-z0-9-]*$"))
            return BadRequest(new { error = "Code may contain lowercase letters, numbers and hyphens only." });
        if (await db.IndustryPacks.AnyAsync(x => x.Id != id && x.Code == code, ct))
            return Conflict(new { error = "An Industry Pack with this code already exists." });
        var template = string.IsNullOrWhiteSpace(input.TemplateJson) ? "{}" : input.TemplateJson!;
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(template);
            if (json.RootElement.ValueKind != System.Text.Json.JsonValueKind.Object)
                return BadRequest(new { error = "TemplateJson must be a JSON object." });
        }
        catch (System.Text.Json.JsonException)
        {
            return BadRequest(new { error = "TemplateJson is not valid JSON." });
        }
        pack.Code = code;
        pack.Name = name;
        pack.Description = input.Description?.Trim() ?? string.Empty;
        pack.TemplateJson = template;
        pack.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(pack);
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
