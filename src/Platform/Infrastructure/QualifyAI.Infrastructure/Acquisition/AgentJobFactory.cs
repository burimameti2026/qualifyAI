using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

/// <summary>
/// Creates a first-class AgentJob for a campaign while preserving the legacy
/// AgentRun as the current execution adapter until the migration is complete.
/// </summary>
public interface IAgentJobFactory
{
    Task<AgentJob> QueueCampaignAsync(
        Guid tenantId,
        Guid campaignId,
        Guid agentId,
        Guid? containerId,
        string type,
        string query,
        bool isManual,
        CancellationToken ct);
}

public sealed class AgentJobFactory(AppDbContext db) : IAgentJobFactory
{
    public async Task<AgentJob> QueueCampaignAsync(
        Guid tenantId,
        Guid campaignId,
        Guid agentId,
        Guid? containerId,
        string type,
        string query,
        bool isManual,
        CancellationToken ct)
    {
        var existing = await db.AgentJobs
            .Where(x => x.TenantId == tenantId &&
                        x.CampaignId == campaignId &&
                        x.ContainerId == containerId &&
                        x.Status is AgentJobStatus.Queued or AgentJobStatus.Running or AgentJobStatus.Waiting)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
            return existing;

        var legacyRun = new AutonomousAcquisitionAgentRun
        {
            TenantId = tenantId,
            AgentId = agentId,
            IsManual = isManual,
            Status = AutonomousAgentRunStatus.Queued,
            Query = query
        };

        db.AutonomousAcquisitionAgentRuns.Add(legacyRun);

        var job = new AgentJob
        {
            TenantId = tenantId,
            CampaignId = campaignId,
            ContainerId = containerId,
            AgentId = agentId,
            Type = type,
            Sequence = 1,
            Status = AgentJobStatus.Queued,
            PayloadJson = JsonSerializer.Serialize(new { runId = legacyRun.Id, query }),
            ScheduledAtUtc = DateTime.UtcNow
        };

        db.AgentJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }
}
