using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Analytics)]
[RequirePermission(QualifyAiPermissions.AnalyticsRead)]
[Route("api/analytics")]
public sealed class AnalyticsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("overview")]
    public Task<AnalyticsOverviewDto> Overview(CancellationToken ct) => sender.Send(new GetAnalyticsOverviewQuery(tenant.TenantId()), ct);
}
