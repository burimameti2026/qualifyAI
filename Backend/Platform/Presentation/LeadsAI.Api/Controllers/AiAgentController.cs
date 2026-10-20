using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LeadsAI.Application;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Ai)]
[RequirePermission(QualifyAiPermissions.AgentsRead)]
[Route("api/ai/agent")]
public sealed class AiAgentController(IAiAgent agent, ITenantContext tenant) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Run([FromBody] AgentRunRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Goal))
            return BadRequest(new { detail = "Goal is required." });

        var userId = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : (Guid?)null;
        var result = await agent.RunAsync(
            new AiAgentRequest(input.Goal.Trim(), input.ContextJson),
            new AiToolContext(tenant.TenantId(), userId, null),
            ct);

        return Ok(result);
    }
}

public sealed record AgentRunRequest(string Goal, string? ContextJson = null);
