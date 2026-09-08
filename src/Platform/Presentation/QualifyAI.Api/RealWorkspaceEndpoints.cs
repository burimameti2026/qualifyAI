using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Infrastructure.Acquisition;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public static class RealWorkspaceEndpoints
{
    public static IEndpointRouteBuilder MapRealWorkspace(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/real-workspace");

        g.MapGet("/options", () => Results.Ok(new
        {
            useCases = new[] { "autonomous-acquisition" },
            schedule = "daily"
        }));

        g.MapPost("/prepare", async (RealWorkspaceRequest request, AppDbContext db, IAutonomousAcquisitionTemplateRegistry templates, CancellationToken ct) =>
        {
            if (request.TenantId == Guid.Empty) return Results.BadRequest(new { error = "tenantId is required" });
            var agent = await EnsureAgent(request, db, templates, ct);
            return Results.Ok(new RealWorkspaceResult(request.TenantId, agent.Id, agent.Name, agent.Status.ToString(), null, "prepared"));
        });

        g.MapPost("/activate", async (RealWorkspaceRequest request, AppDbContext db, IAutonomousAcquisitionTemplateRegistry templates, CancellationToken ct) =>
        {
            if (request.TenantId == Guid.Empty) return Results.BadRequest(new { error = "tenantId is required" });

            var agent = await EnsureAgent(request, db, templates, ct);
            agent.Status = AutonomousAgentStatus.Active;
            agent.UpdatedAtUtc = DateTime.UtcNow;

            var existingQueuedOrRunning = await db.AutonomousAcquisitionAgentRuns.AnyAsync(x =>
                x.TenantId == request.TenantId && x.AgentId == agent.Id &&
                (x.Status == AutonomousAgentRunStatus.Queued || x.Status == AutonomousAgentRunStatus.Running), ct);

            AutonomousAcquisitionAgentRun? initialRun = null;
            if (!existingQueuedOrRunning)
            {
                initialRun = new AutonomousAcquisitionAgentRun
                {
                    TenantId = request.TenantId,
                    AgentId = agent.Id,
                    IsManual = true,
                    Status = AutonomousAgentRunStatus.Queued,
                    ScheduledAtUtc = DateTime.UtcNow
                };
                db.AutonomousAcquisitionAgentRuns.Add(initialRun);
            }

            await db.SaveChangesAsync(ct);

            return Results.Accepted($"/api/real-workspace/tenants/{request.TenantId}",
                new RealWorkspaceResult(request.TenantId, agent.Id, agent.Name, agent.Status.ToString(), initialRun?.Id, "activation-queued"));
        });

        return app;
    }

    private static async Task<AutonomousAcquisitionAgent> EnsureAgent(
        RealWorkspaceRequest request,
        AppDbContext db,
        IAutonomousAcquisitionTemplateRegistry templates,
        CancellationToken ct)
    {
        var agent = await db.AutonomousAcquisitionAgents
            .OrderByDescending(x => x.UpdatedAtUtc)
            .FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.Name == request.Name, ct);

        if (agent is null)
        {
            agent = new AutonomousAcquisitionAgent
            {
                Id = Guid.NewGuid(),
                TenantId = request.TenantId,
                Name = string.IsNullOrWhiteSpace(request.Name) ? "Real Workspace Acquisition" : request.Name.Trim(),
                TemplateKey = request.TemplateKey ?? string.Empty,
                Industry = request.Industry ?? string.Empty,
                Region = request.Region ?? string.Empty,
                CountriesJson = request.CountriesJson ?? "[]",
                DailyDiscoveryLimit = request.DailyDiscoveryLimit > 0 ? request.DailyDiscoveryLimit : 25,
                MinimumScore = request.MinimumScore > 0 ? request.MinimumScore : 70,
                RunTimeUtc = request.RunTimeUtc ?? new TimeOnly(8, 0),
                Status = AutonomousAgentStatus.Draft,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };
            templates.Apply(agent);
            db.AutonomousAcquisitionAgents.Add(agent);
        }
        else
        {
            if (!string.IsNullOrWhiteSpace(request.TemplateKey)) agent.TemplateKey = request.TemplateKey;
            if (!string.IsNullOrWhiteSpace(request.Industry)) agent.Industry = request.Industry;
            if (!string.IsNullOrWhiteSpace(request.Region)) agent.Region = request.Region;
            if (!string.IsNullOrWhiteSpace(request.CountriesJson)) agent.CountriesJson = request.CountriesJson;
            if (request.DailyDiscoveryLimit > 0) agent.DailyDiscoveryLimit = request.DailyDiscoveryLimit;
            if (request.MinimumScore > 0) agent.MinimumScore = request.MinimumScore;
            if (request.RunTimeUtc.HasValue) agent.RunTimeUtc = request.RunTimeUtc.Value;
            agent.UpdatedAtUtc = DateTime.UtcNow;
            templates.Apply(agent);
        }

        await db.SaveChangesAsync(ct);
        return agent;
    }
}

public sealed record RealWorkspaceRequest(
    Guid TenantId,
    string? Name,
    string? UseCase,
    string? TemplateKey,
    string? Industry,
    string? Region,
    string? CountriesJson,
    int DailyDiscoveryLimit = 25,
    int MinimumScore = 70,
    TimeOnly? RunTimeUtc = null);

public sealed record RealWorkspaceResult(Guid TenantId, Guid AgentId, string AgentName, string AgentStatus, Guid? InitialRunId, string Status);
