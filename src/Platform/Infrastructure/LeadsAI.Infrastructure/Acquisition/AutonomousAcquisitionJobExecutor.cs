using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain.Core;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

public sealed class AutonomousAcquisitionJobExecutor(
    AppDbContext db,
    IAutonomousAcquisitionJobOrchestrator orchestrator) : IAgentJobExecutor
{
    public async Task ExecuteAsync(AgentJob job, CancellationToken ct)
    {
        try
        {
            await orchestrator.ExecuteAsync(job, ct);

            if (job.Status == AgentJobStatus.Running)
            {
                job.Status = AgentJobStatus.Completed;
                job.CompletedAtUtc = DateTime.UtcNow;
            }

            job.Error = null;
            job.LeaseUntilUtc = null;
            job.WorkerId = null;
            job.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            job.Status = job.AttemptCount < job.MaxAttempts
                ? AgentJobStatus.Queued
                : AgentJobStatus.Failed;
            job.Error = ex.Message;
            job.LeaseUntilUtc = null;
            job.WorkerId = null;
            job.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
    }
}
