using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Infrastructure;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/provisioning")]
public sealed class TenantProvisioningController(AppDbContext db, IModuleProvisioningOrchestrator provisioning) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenantExists = await db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!tenantExists) return NotFound();
        var modules = await db.TenantModuleProvisionings.Where(x => x.TenantId == tenantId).OrderBy(x => x.ModuleCode).ToListAsync(cancellationToken);
        return Ok(new { tenantId, modules = modules.Select(x => new { x.ModuleCode, x.Status, x.AttemptCount, x.LastError, x.LastAttemptAtUtc, x.CompletedAtUtc, x.NextRetryAtUtc, x.UpdatedAtUtc }) });
    }

    [HttpPost("{moduleCode}/retry")]
    public async Task<IActionResult> Retry(Guid tenantId, string moduleCode, CancellationToken cancellationToken)
    {
        var exists = await db.Tenants.AnyAsync(x => x.Id == tenantId, cancellationToken);
        if (!exists) return NotFound();
        await provisioning.ProvisionAsync(tenantId, new[] { moduleCode }, cancellationToken);
        return Accepted(new { tenantId, moduleCode, status = "retry_requested" });
    }

    [HttpPost("retry-failed")]
    public async Task<IActionResult> RetryFailed(Guid tenantId, CancellationToken cancellationToken)
    {
        var failed = await db.TenantModuleProvisionings.Where(x => x.TenantId == tenantId && x.Status == "failed").Select(x => x.ModuleCode).ToArrayAsync(cancellationToken);
        if (failed.Length == 0) return Ok(new { tenantId, status = "nothing_to_retry" });
        await provisioning.ProvisionAsync(tenantId, failed, cancellationToken);
        return Accepted(new { tenantId, modules = failed, status = "retry_requested" });
    }
}
