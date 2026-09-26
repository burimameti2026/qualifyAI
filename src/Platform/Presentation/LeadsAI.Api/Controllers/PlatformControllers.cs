using LeadsAI.Infrastructure.IndustryPacks;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Commands.Modules;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Billing)]
[RequirePermission(QualifyAiPermissions.BillingRead)]
[Route("api/billing")]
public sealed class BillingController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("plans")]
    public Task<IReadOnlyList<Plan>> Plans(CancellationToken ct) => sender.Send(new ListBillingPlansQuery(tenant.TenantId()), ct);

    [HttpGet("usage")]
    public Task<IReadOnlyList<UsageMeterDto>> Usage(CancellationToken ct) => sender.Send(new GetBillingUsageQuery(tenant.TenantId()), ct);

    [HttpGet("summary")]
    public async Task<IActionResult> Summary(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var plans = await sender.Send(new ListBillingPlansQuery(tenantId), ct);
        var usage = await sender.Send(new GetBillingUsageQuery(tenantId), ct);
        return Ok(new { plans, usage, generatedAtUtc = DateTime.UtcNow });
    }
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Settings)]
[RequirePermission(QualifyAiPermissions.SettingsManage)]
[Route("api/security")]
public sealed class SecurityController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("sso")]
    public Task<IReadOnlyList<SsoConfiguration>> Sso(CancellationToken ct) => sender.Send(new ListSsoConfigurationsQuery(tenant.TenantId()), ct);

    [HttpGet("retention")]
    public Task<IReadOnlyList<DataRetentionPolicy>> Retention(CancellationToken ct) => sender.Send(new ListRetentionPoliciesQuery(tenant.TenantId()), ct);
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Settings)]
[Route("api/white-label")]
public sealed class WhiteLabelController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("branding")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public Task<BrandingProfile?> Branding(CancellationToken ct) => sender.Send(new GetBrandingQuery(tenant.TenantId()), ct);

    [HttpPut("branding")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public Task<BrandingProfile> UpdateBranding(BrandingProfile input, CancellationToken ct) => sender.Send(new UpdateBrandingCommand(tenant.TenantId(), input), ct);

    [HttpGet("domains")]
    [RequirePermission(QualifyAiPermissions.SettingsManage)]
    public Task<IReadOnlyList<CustomDomain>> Domains(CancellationToken ct) => sender.Send(new ListCustomDomainsQuery(tenant.TenantId()), ct);
}




[ApiController]
[Authorize]
[RequirePermission(QualifyAiPermissions.AuditRead)]
[Route("api/platform")]
public sealed class PlatformController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("audit")]
    public Task<IReadOnlyList<AuditLog>> Audit(CancellationToken ct) => sender.Send(new ListAuditLogsQuery(tenant.TenantId()), ct);
}

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Analytics)]
[RequirePermission(QualifyAiPermissions.AnalyticsRead)]
[Route("api/revenue")]
public sealed class RevenueController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("attribution")]
    public Task<IReadOnlyList<RevenueAttribution>> Attribution(CancellationToken ct) => sender.Send(new ListRevenueAttributionQuery(tenant.TenantId()), ct);
}


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
        var packs = await db.IndustryPacks.AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var tenantId = tenant.TenantId();
        var installed = await db.TenantIndustryPacks.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Enabled)
            .Select(x => x.IndustryPackId)
            .ToListAsync(ct);

        var provisioned = await db.Campaigns.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.PackageCode != null && x.PackageCode != string.Empty)
            .Select(x => new { x.Id, x.PackageCode })
            .ToListAsync(ct);

        return Ok(packs.Select(pack =>
        {
            var marker = $"industry-pack:{pack.Code.Trim().ToLowerInvariant()}";
            var campaign = provisioned.FirstOrDefault(x => x.PackageCode == marker);
            return new
            {
                pack.Id,
                pack.Code,
                pack.Name,
                pack.Description,
                pack.TemplateJson,
                installed = installed.Contains(pack.Id),
                provisioned = campaign is not null,
                campaignId = campaign?.Id
            };
        }));
    }

    [HttpPost]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Create([FromBody] IndustryPack input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { detail = "Industry Pack code and name are required." });

        var code = input.Code.Trim().ToLowerInvariant();
        if (await db.IndustryPacks.AnyAsync(x => x.Code == code, ct))
            return Conflict(new { detail = $"Industry Pack code '{code}' already exists." });

        var pack = new IndustryPack
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = input.Name.Trim(),
            Description = input.Description?.Trim() ?? string.Empty,
            TemplateJson = string.IsNullOrWhiteSpace(input.TemplateJson) ? "{}" : input.TemplateJson
        };

        db.IndustryPacks.Add(pack);
        await db.SaveChangesAsync(ct);
        return Created($"/api/industry-packs/{pack.Id}", pack);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] IndustryPack input, CancellationToken ct)
    {
        var pack = await db.IndustryPacks.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (pack is null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name))
            return BadRequest(new { detail = "Industry Pack code and name are required." });

        var code = input.Code.Trim().ToLowerInvariant();
        if (await db.IndustryPacks.AnyAsync(x => x.Id != id && x.Code == code, ct))
            return Conflict(new { detail = $"Industry Pack code '{code}' already exists." });

        pack.Code = code;
        pack.Name = input.Name.Trim();
        pack.Description = input.Description?.Trim() ?? string.Empty;
        pack.TemplateJson = string.IsNullOrWhiteSpace(input.TemplateJson) ? "{}" : input.TemplateJson;
        pack.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return Ok(pack);
    }

    [HttpPost("{id:guid}/provision")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Provision(Guid id, [FromBody] IndustryPackProvisionRequest? input, CancellationToken ct)
    {
        try
        {
            var result = await provisioner.ProvisionAsync(
                tenant.TenantId(), id, ct, input?.ScenarioCode, input?.IcpProfileId);
            return Ok(new
            {
                result.IndustryPackId,
                result.IndustryCode,
                result.TargetListId,
                result.CampaignId,
                result.CampaignStatus,
                result.ProvisioningMode,
                result.Definition,
                result.AlreadyProvisioned
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    [HttpPost("{id:guid}/install")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public Task<IActionResult> Install(Guid id, CancellationToken ct)
        => Provision(id, null, ct);
}

public sealed record IndustryPackProvisionRequest(string? ScenarioCode, Guid? IcpProfileId);
