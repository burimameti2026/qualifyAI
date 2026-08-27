using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Commands;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Modules;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/sales")]
public sealed class SalesController(ISender sender, ITenantContext tenant, SalesAutomationService sales) : ControllerBase
{
    [HttpGet("pipelines")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<SalesPipelinesDto> Pipelines(CancellationToken ct) => sender.Send(new GetSalesPipelinesQuery(tenant.TenantId()), ct);

    [HttpPost("automation/run")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> RunAutomation(CancellationToken ct) => Ok(await sales.RunAsync(tenant.TenantId(), ct));

    [HttpPost("leads/{id:guid}/convert")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Convert(Guid id, CancellationToken ct)
        => (await sales.ConvertLeadAsync(tenant.TenantId(), id, ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("leads/{id:guid}/qualify")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Qualify(Guid id, CancellationToken ct)
        => (await sender.Send(new QualifyLeadCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();

    [HttpGet("tasks")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public Task<IReadOnlyList<CrmTask>> Tasks(CancellationToken ct) => sender.Send(new ListSalesTasksQuery(tenant.TenantId()), ct);

    [HttpPut("tasks/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> UpdateTask(Guid id, CrmTask input, CancellationToken ct)
        => (await sender.Send(new UpdateSalesTaskCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();
}

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
