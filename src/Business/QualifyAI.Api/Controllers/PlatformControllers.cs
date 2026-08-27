using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Modules;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Billing)]
[RequirePermission(QualifyAiPermissions.BillingRead)]
[Route("api/billing")]
public sealed class BillingController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("plans")]
    public Task<IReadOnlyList<Plan>> Plans(CancellationToken ct) => sender.Send(new ListBillingPlansQuery(), ct);

    [HttpGet("usage")]
    public Task<IReadOnlyList<UsageMeterDto>> Usage(CancellationToken ct) => sender.Send(new GetBillingUsageQuery(tenant.TenantId()), ct);
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
public sealed class IndustryPacksController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<IndustryPack>> List(CancellationToken ct) => sender.Send(new ListIndustryPacksQuery(), ct);

    [HttpPost("{id:guid}/install")]
    public async Task<IActionResult> Install(Guid id, CancellationToken ct)
        => await sender.Send(new InstallIndustryPackCommand(tenant.TenantId(), id), ct) ? Ok(new { installed = true }) : NotFound();
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
