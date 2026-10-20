using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;


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
