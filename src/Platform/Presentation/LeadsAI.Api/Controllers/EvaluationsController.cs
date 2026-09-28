using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Commands;
using LeadsAI.Application.Commands.Modules;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using LeadsAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Ai)]
[RequirePermission(QualifyAiPermissions.AgentsRead)]
[Route("api/evaluations")]
public sealed class EvaluationsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("datasets")]
    public Task<IReadOnlyList<EvaluationDataset>> Datasets(CancellationToken ct) => sender.Send(new ListEvaluationDatasetsQuery(tenant.TenantId()), ct);
}

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
