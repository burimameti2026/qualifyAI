using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QualifyAI.Application;
using QualifyAI.Application.Commands.Modules;
using QualifyAI.Application.Queries.Modules;
using QualifyAI.Domain;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/knowledge")]
public sealed class KnowledgeController(ISender sender, ITenantContext tenant, IKnowledgeRetriever retriever) : ControllerBase
{
    [HttpGet("bases")]
    public Task<IReadOnlyList<KnowledgeBase>> Bases(CancellationToken ct) => sender.Send(new ListKnowledgeBasesQuery(tenant.TenantId()), ct);

    [HttpGet("documents")]
    public Task<IReadOnlyList<KnowledgeDocument>> Documents(CancellationToken ct) => sender.Send(new ListKnowledgeDocumentsQuery(tenant.TenantId()), ct);

    [HttpPost("documents")]
    public async Task<IActionResult> CreateDocument(KnowledgeDocument input, CancellationToken ct) => Ok(await sender.Send(new CreateKnowledgeDocumentCommand(tenant.TenantId(), input), ct));

    [HttpPut("documents/{id:guid}")]
    public async Task<IActionResult> UpdateDocument(Guid id, KnowledgeDocument input, CancellationToken ct)
        => (await sender.Send(new UpdateKnowledgeDocumentCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("documents/{id:guid}/reindex")]
    public async Task<IActionResult> Reindex(Guid id, CancellationToken ct)
        => (await sender.Send(new ReindexKnowledgeDocumentCommand(tenant.TenantId(), id), ct)) is { } x ? Ok(x) : NotFound();

    [HttpGet("gaps")]
    public Task<IReadOnlyList<KnowledgeGap>> Gaps(CancellationToken ct) => sender.Send(new ListKnowledgeGapsQuery(tenant.TenantId()), ct);

    [HttpPut("gaps/{id:guid}")]
    public async Task<IActionResult> UpdateGap(Guid id, KnowledgeGap input, CancellationToken ct)
        => (await sender.Send(new UpdateKnowledgeGapCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("retrieve")]
    public async Task<IActionResult> Retrieve(RetrieveInput input, CancellationToken ct)
    {
        var result = await retriever.SearchAsync(tenant.TenantId(), input.Query, 5);
        return Ok(new { answer = string.Join("\n", result.Select(x => x.Text)), sources = result });
    }
}

[ApiController]
[Authorize]
[Route("api/ai")]
public sealed class AiController(ISender sender, ITenantContext tenant, IAiToolRegistry tools, IAiProvider ai) : ControllerBase
{
    [HttpGet("agents")]
    public Task<IReadOnlyList<AiAgent>> Agents(CancellationToken ct) => sender.Send(new ListAiAgentsQuery(tenant.TenantId()), ct);

    [HttpPost("agents")]
    public async Task<IActionResult> CreateAgent(AiAgent input, CancellationToken ct) => Ok(await sender.Send(new CreateAiAgentCommand(tenant.TenantId(), input), ct));

    [HttpPut("agents/{id:guid}")]
    public async Task<IActionResult> UpdateAgent(Guid id, AiAgent input, CancellationToken ct)
        => (await sender.Send(new UpdateAiAgentCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("agents/{id:guid}/test")]
    public async Task<IActionResult> TestAgent(Guid id, AgentTestInput input, CancellationToken ct)
    {
        var agent = (await sender.Send(new ListAiAgentsQuery(tenant.TenantId()), ct)).FirstOrDefault(x => x.Id == id);
        if (agent is null) return NotFound();
        var response = await ai.CompleteAsync(agent.Instructions, input.Message, ct);
        return Ok(new { message = response, agent = agent.Name, model = agent.Model });
    }

    [HttpGet("tools")]
    public IActionResult ToolNames() => Ok(tools.Names);

    [HttpPost("tools/{name}/execute")]
    public async Task<IActionResult> ExecuteTool(string name, [FromBody] string input, CancellationToken ct)
    {
        var tool = tools.Resolve(name);
        if (tool is null) return NotFound();
        Guid? userId = Guid.TryParse(User.FindFirst("sub")?.Value, out var parsed) ? parsed : null;
        return Ok(await tool.ExecuteAsync(new(tenant.TenantId(), userId, null), input, ct));
    }
}

public sealed record RetrieveInput(string Query);
public sealed record AgentTestInput(string Message);
