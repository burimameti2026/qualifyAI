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
[RequireModule(QualifyAiModules.Settings)]
[RequirePermission(QualifyAiPermissions.SettingsManage)]
[Route("api/industry-packs")]
public sealed class IndustryPacksController(ISender sender, ITenantContext tenant, AppDbContext db) : ControllerBase
{
    [HttpGet]
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
    public async Task<IActionResult> Install(Guid id, CancellationToken ct)
        => await sender.Send(new InstallIndustryPackCommand(tenant.TenantId(), id), ct)
            ? Ok(new { installed = true })
            : NotFound();
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
