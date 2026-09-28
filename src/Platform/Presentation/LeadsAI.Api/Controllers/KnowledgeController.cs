using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application;
using LeadsAI.Application.Commands.Modules;
using LeadsAI.Application.Queries.Modules;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Infrastructure;
using DomainAiAgent = LeadsAI.Domain.AiAgent;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Knowledge)]
[Route("api/knowledge")]
public sealed class KnowledgeController(ISender sender, ITenantContext tenant, IKnowledgeRetriever retriever) : ControllerBase
{
    [HttpGet("bases")]
    [RequirePermission(QualifyAiPermissions.KnowledgeRead)]
    public Task<IReadOnlyList<KnowledgeBase>> Bases(CancellationToken ct) => sender.Send(new ListKnowledgeBasesQuery(tenant.TenantId()), ct);

    [HttpGet("documents")]
    [RequirePermission(QualifyAiPermissions.KnowledgeRead)]
    public Task<IReadOnlyList<KnowledgeDocument>> Documents(CancellationToken ct) => sender.Send(new ListKnowledgeDocumentsQuery(tenant.TenantId()), ct);

    [HttpPost("documents")]
    [RequirePermission(QualifyAiPermissions.KnowledgeManage)]
    public async Task<IActionResult> CreateDocument(KnowledgeDocument input, CancellationToken ct) => Ok(await sender.Send(new CreateKnowledgeDocumentCommand(tenant.TenantId(), input), ct));

    [HttpPut("documents/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.KnowledgeManage)]
    public async Task<IActionResult> UpdateDocument(Guid id, KnowledgeDocument input, CancellationToken ct)
        => (await sender.Send(new UpdateKnowledgeDocumentCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("documents/{id:guid}/reindex")]
    [RequirePermission(QualifyAiPermissions.KnowledgeManage)]
    public async Task<IActionResult> Reindex(Guid id, CancellationToken ct)
        => (await sender.Send(new ReindexKnowledgeDocumentCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();

    [HttpGet("gaps")]
    [RequirePermission(QualifyAiPermissions.KnowledgeRead)]
    public Task<IReadOnlyList<KnowledgeGap>> Gaps(CancellationToken ct) => sender.Send(new ListKnowledgeGapsQuery(tenant.TenantId()), ct);

    [HttpPut("gaps/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.KnowledgeManage)]
    public async Task<IActionResult> UpdateGap(Guid id, KnowledgeGap input, CancellationToken ct)
        => (await sender.Send(new UpdateKnowledgeGapCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("retrieve")]
    [RequirePermission(QualifyAiPermissions.KnowledgeRead)]
    public async Task<IActionResult> Retrieve(RetrieveInput input, CancellationToken ct)
    {
        var result = await retriever.SearchAsync(tenant.TenantId(), input.Query, 5);
        return Ok(new { answer = string.Join("\n", result.Select(x => x.Text)), sources = result });
    }
}
