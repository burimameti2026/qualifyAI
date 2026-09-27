using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tenant-runtime")]
public sealed class TenantRuntimeController(ITenantContext tenant, ITenantEntitlementRepository entitlements) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var snapshot = await entitlements.GetAsync(tenantId, ct);
        return Ok(new
        {
            tenantId,
            tenantSlug = snapshot?.TenantSlug,
            status = snapshot?.TenantStatus ?? "unknown",
            plan = snapshot?.LicensePlan,
            licenseStatus = snapshot?.LicenseStatus,
            maxUsers = snapshot?.MaxUsers ?? 0,
            startsAtUtc = snapshot?.StartsAtUtc,
            expiresAtUtc = snapshot?.ExpiresAtUtc,
            version = snapshot?.Version ?? 0,
            modules = snapshot?.EnabledModules ?? Array.Empty<string>(),
            limits = snapshot?.Limits ?? new Dictionary<string, int>(),
            updatedAtUtc = snapshot?.UpdatedAtUtc
        });
    }
}
