using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

public interface IAgentJobQueue
{
    Task<AgentJob?> GetNextJobAndClaimAsync(Guid tenantId, string workerId, CancellationToken ct);
}

public sealed class AgentJobQueue(AppDbContext db) : IAgentJobQueue
{
    public async Task<AgentJob?> GetNextJobAndClaimAsync(
        Guid tenantId,
        string workerId,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // SQL Server locking makes the claim atomic across workers in the same tenant pool.
        var job = await db.AgentJobs
            .FromSqlInterpolated($"""
                SELECT TOP (1) *
                FROM [AgentJobs] WITH (UPDLOCK, READPAST, ROWLOCK)
                WHERE [TenantId] = {tenantId}
                  AND [Status] = {"Queued"}
                  AND [ScheduledAtUtc] <= {DateTime.UtcNow}
                ORDER BY [Priority] DESC, [ScheduledAtUtc], [CreatedAtUtc]
                """)
            .SingleOrDefaultAsync(ct);

        if (job is null)
        {
            await tx.RollbackAsync(ct);
            return null;
        }

        var now = DateTime.UtcNow;
        job.Status = AgentJobStatus.Running;
        job.ClaimedAtUtc = now;
        job.StartedAtUtc ??= now;
        job.LeaseUntilUtc = now.AddMinutes(10);
        job.WorkerId = workerId;
        job.AttemptCount++;
        job.UpdatedAtUtc = now;

        if (job.ContainerId.HasValue)
        {
            var container = await db.CampaignContainers
                .SingleOrDefaultAsync(x => x.TenantId == tenantId &&
                                           x.Id == job.ContainerId.Value, ct);

            if (container is not null)
            {
                container.Status = CampaignContainerStatus.Running;
                container.LastStartedAtUtc ??= now;
                container.LastStoppedAtUtc = null;
                container.UpdatedAtUtc = now;
            }
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return job;
    }
}
