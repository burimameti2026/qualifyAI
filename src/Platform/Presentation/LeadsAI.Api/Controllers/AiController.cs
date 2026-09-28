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
[RequireModule(QualifyAiModules.Ai)]
[Route("api/ai")]
public sealed class AiController(ISender sender, ITenantContext tenant, IAiToolRegistry tools, IAiProvider ai) : ControllerBase
{
    [HttpGet("agents")]
    [RequirePermission(QualifyAiPermissions.AgentsRead)]
    public Task<IReadOnlyList<DomainAiAgent>> Agents(CancellationToken ct) => sender.Send(new ListAiAgentsQuery(tenant.TenantId()), ct);

    [HttpPost("agents")]
    [RequirePermission(QualifyAiPermissions.AgentsManage)]
    public async Task<IActionResult> CreateAgent(DomainAiAgent input, CancellationToken ct) => Ok(await sender.Send(new CreateAiAgentCommand(tenant.TenantId(), input), ct));

    [HttpPut("agents/{id:guid}")]
    [RequirePermission(QualifyAiPermissions.AgentsManage)]
    public async Task<IActionResult> UpdateAgent(Guid id, DomainAiAgent input, CancellationToken ct)
        => (await sender.Send(new UpdateAiAgentCommand(tenant.TenantId(), id, input), ct)) is { } x ? Ok(x) : NotFound();

    [HttpPost("agents/{id:guid}/test")]
    [RequirePermission(QualifyAiPermissions.AgentsManage)]
    public async Task<IActionResult> TestAgent(Guid id, AgentTestInput input, CancellationToken ct)
    {
        var agent = (await sender.Send(new ListAiAgentsQuery(tenant.TenantId()), ct)).FirstOrDefault(x => x.Id == id);
        if (agent is null) return NotFound();
        var response = await ai.CompleteAsync(agent.Instructions, input.Message, ct);
        return Ok(new { message = response, agent = agent.Name, model = agent.Model });
    }

    [HttpGet("tools")]
    [RequirePermission(QualifyAiPermissions.AgentsRead)]
    public IActionResult ToolNames() => Ok(tools.Names);

    [HttpPost("tools/{name}/execute")]
    [RequirePermission(QualifyAiPermissions.AgentsManage)]
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
