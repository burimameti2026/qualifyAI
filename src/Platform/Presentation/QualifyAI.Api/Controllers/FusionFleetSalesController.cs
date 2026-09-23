using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.Domain;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/fusionfleet/sales")]
public sealed class FusionFleetSalesController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private static readonly string[] Markets = ["Germany", "France", "Italy"];

    [HttpGet]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var agents = await db.AutonomousAcquisitionAgents.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.Name.StartsWith("FusionFleet — "))
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        var runs = await db.AutonomousAcquisitionAgentRuns.AsNoTracking()
            .Where(x => x.TenantId == tenantId && agents.Select(a => a.Id).Contains(x.AgentId))
            .OrderByDescending(x => x.ScheduledAtUtc)
            .ToListAsync(ct);

        return Ok(Markets.Select(country =>
        {
            var agent = agents.FirstOrDefault(x => x.CountriesJson.Contains(country, StringComparison.OrdinalIgnoreCase));
            var run = agent is null ? null : runs.FirstOrDefault(x => x.AgentId == agent.Id);
            return new
            {
                country,
                agentId = agent?.Id,
                status = agent?.Status.ToString() ?? "NotConfigured",
                discovered = run?.DiscoveredCount ?? 0,
                qualified = run?.QualifiedCount ?? 0,
                highScore = run?.HighScoreCount ?? 0,
                emailsSent = run?.EmailsSentCount ?? 0
            };
        }));
    }

    [HttpPost("activate")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> Activate(CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var result = new List<object>();

        foreach (var country in Markets)
        {
            var name = $"FusionFleet — {country} Logistics";
            var agent = await db.AutonomousAcquisitionAgents
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Name == name, ct);

            if (agent is null)
            {
                agent = new AutonomousAcquisitionAgent
                {
                    TenantId = tenantId,
                    Name = name,
                    TemplateCode = "logistics",
                    Industry = "Logistics & Transport",
                    Region = "Europe",
                    CountriesJson = JsonSerializer.Serialize(new[] { country }),
                    IcpJson = JsonSerializer.Serialize(new
                    {
                        industries = new[] { "Logistics", "Transportation", "Freight Forwarding", "3PL", "Warehousing", "Distribution" },
                        employeeRange = "20-1000",
                        decisionMakerTitles = new[] { "CEO", "Owner", "Managing Director", "Sales Director", "Commercial Director", "Operations Director", "Fleet Manager", "Logistics Director" },
                        minimumScore = 70
                    }),
                    MinimumScore = 70,
                    DailyDiscoveryLimit = 50,
                    DailyEmailLimit = 0,
                    RunTimeUtc = new TimeOnly(8, 0),
                    Status = AutonomousAgentStatus.Active,
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow
                };
                db.AutonomousAcquisitionAgents.Add(agent);
            }
            else
            {
                agent.TemplateCode = "logistics";
                agent.Industry = "Logistics & Transport";
                agent.Region = "Europe";
                agent.CountriesJson = JsonSerializer.Serialize(new[] { country });
                agent.MinimumScore = 70;
                agent.DailyDiscoveryLimit = 50;
                agent.DailyEmailLimit = 0;
                agent.Status = AutonomousAgentStatus.Active;
                agent.UpdatedAtUtc = DateTime.UtcNow;
            }

            result.Add(new { country, agentId = agent.Id, status = agent.Status.ToString() });
        }

        await db.SaveChangesAsync(ct);
        return Ok(new { tenantId, markets = result, discoveryOnly = true });
    }
}
