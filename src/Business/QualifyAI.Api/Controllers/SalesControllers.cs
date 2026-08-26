using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application.Commands;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Modules;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/sales")]
public sealed class SalesController(ISender sender, ITenantContext tenant, SalesAutomationService sales) : ControllerBase
{
    [HttpGet("pipelines")]
    public Task<SalesPipelinesDto> Pipelines(CancellationToken ct) => sender.Send(new GetSalesPipelinesQuery(tenant.TenantId()), ct);

    [HttpPost("automation/run")]
    public async Task<IActionResult> RunAutomation(CancellationToken ct) => Ok(await sales.RunAsync(tenant.TenantId(), ct));

    [HttpPost("leads/{id:guid}/convert")]
    public async Task<IActionResult> Convert(Guid id, CancellationToken ct)
        => (await sales.ConvertLeadAsync(tenant.TenantId(), id, ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("leads/{id:guid}/qualify")]
    public async Task<IActionResult> Qualify(Guid id, CancellationToken ct)
        => (await sender.Send(new QualifyLeadCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();

    [HttpGet("tasks")]
    public Task<IReadOnlyList<CrmTask>> Tasks(CancellationToken ct) => sender.Send(new ListSalesTasksQuery(tenant.TenantId()), ct);

    [HttpPut("tasks/{id:guid}")]
    public async Task<IActionResult> UpdateTask(Guid id, CrmTask input, CancellationToken ct)
        => (await sender.Send(new UpdateSalesTaskCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();
}

[ApiController]
[Authorize]
[Route("api/evaluations")]
public sealed class EvaluationsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("datasets")]
    public Task<IReadOnlyList<EvaluationDataset>> Datasets(CancellationToken ct) => sender.Send(new ListEvaluationDatasetsQuery(tenant.TenantId()), ct);
}

[ApiController]
[Authorize]
[Route("api/analytics")]
public sealed class AnalyticsController(ISender sender, ITenantContext tenant) : ControllerBase
{
    [HttpGet("overview")]
    public Task<AnalyticsOverviewDto> Overview(CancellationToken ct) => sender.Send(new GetAnalyticsOverviewQuery(tenant.TenantId()), ct);
}
