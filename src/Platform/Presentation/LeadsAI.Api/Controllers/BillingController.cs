using LeadsAI.Infrastructure.IndustryPacks;
using LeadsAI.Application;
using System.Text.Json;
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
