using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Persistence.SqlServer;
using QualifyAI.Infrastructure;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/real-workspace")]
public sealed class RealWorkspaceAutomationController(AppDbContext db, ITenantContext tenant, IAutonomousAcquisitionTemplateRegistry templates) : ControllerBase
{
    public sealed record Request(string? Name, string? UseCase, string? TemplateKey, string? Industry, string? Region, string? CountriesJson, int DailyDiscoveryLimit = 25, int MinimumScore = 70, string? RunTimeUtc = null);
    public sealed record Result(Guid TenantId, Guid AgentId, string AgentName, string AgentStatus, Guid? InitialRunId, string Status, Guid? TargetListId, Guid? CampaignId);

    [HttpGet("options")]
    public IActionResult Options() => Ok(new RealWorkspaceService(db).Options());

    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare([FromBody] Request request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        if (string.IsNullOrWhiteSpace(request.UseCase) || string.IsNullOrWhiteSpace(request.TemplateKey) || string.IsNullOrWhiteSpace(request.Industry) || string.IsNullOrWhiteSpace(request.Region))
            return BadRequest(new { detail = "Use case, template, industry and region are required." });

        var template = templates.Resolve(request.TemplateKey);
        if (template is null) return BadRequest(new { detail = "The selected acquisition template is not available." });

        if (!template.UseCaseId.Equals(request.UseCase, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { detail = "The selected template does not belong to the selected use case." });

        var agent = new AutonomousAcquisitionAgent
        {
            TenantId = tenantId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? template.Name : request.Name.Trim(),
            TemplateCode = request.TemplateKey,
            Industry = request.Industry.Trim(),
            Region = request.Region.Trim(),
            CountriesJson = string.IsNullOrWhiteSpace(request.CountriesJson) ? "[]" : request.CountriesJson,
            MinimumScore = Math.Clamp(request.MinimumScore, 1, 100),
            DailyDiscoveryLimit = Math.Clamp(request.DailyDiscoveryLimit, 1, 100),
            Status = AutonomousAgentStatus.Active,
            RunTimeUtc = ParseRunTime(request.RunTimeUtc)
        };
        templates.Apply(agent);
        agent.Status = AutonomousAgentStatus.Active;

        db.AutonomousAcquisitionAgents.Add(agent);
        var run = new AutonomousAcquisitionAgentRun
        {
            TenantId = tenantId,
            AgentId = agent.Id,
            IsManual = false,
            Status = AutonomousAgentRunStatus.Queued,
            ScheduledAtUtc = DateTime.UtcNow
        };
        db.AutonomousAcquisitionAgentRuns.Add(run);
        await db.SaveChangesAsync(ct);

        return Ok(new Result(tenantId, agent.Id, agent.Name, agent.Status.ToString(), run.Id, "activation-queued", null, null));
    }

    private static TimeOnly ParseRunTime(string? value)
        => TimeOnly.TryParse(value, out var time) ? time : new TimeOnly(8, 0);
}
