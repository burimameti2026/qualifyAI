using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

/// <summary>
/// Creates the first-class tenant-scoped AgentJob used as the campaign execution unit.
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
                        (x.Status == AgentJobStatus.Queued || x.Status == AgentJobStatus.Running || x.Status == AgentJobStatus.Waiting))
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);

        if (existing is not null)
            return existing;

        var job = new AgentJob
        {
            TenantId = tenantId,
            CampaignId = campaignId,
            ContainerId = containerId,
            AgentId = agentId,
            Type = type,
            IsManual = isManual,
            Query = query,
            Sequence = 1,
            Status = AgentJobStatus.Queued,
            PayloadJson = JsonSerializer.Serialize(new { query }),
            ScheduledAtUtc = DateTime.UtcNow
        };

        db.AgentJobs.Add(job);
        await db.SaveChangesAsync(ct);
        return job;
    }
}
