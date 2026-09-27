using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

/// <summary>
/// Transitional executor: Job is now the queue/ownership boundary while the
/// existing acquisition business implementation is reused behind it.
/// The old AgentRun is only a compatibility payload and is not claimed by workers.
/// </summary>
public sealed class AutonomousAcquisitionJobExecutor(
    AppDbContext db,
    IAutonomousAcquisitionRunOrchestrator legacyOrchestrator) : IAgentJobExecutor
{
    public async Task ExecuteAsync(AgentJob job, CancellationToken ct)
    {
        try
        {
            var payload = JsonSerializer.Deserialize<JobCompatibilityPayload>(job.PayloadJson)
                ?? new JobCompatibilityPayload();

            if (payload.RunId is Guid runId)
            {
                await legacyOrchestrator.ExecuteAsync(runId, ct);
            }
            else
            {
                throw new InvalidOperationException(
                    $"No execution adapter is registered for Job type '{job.Type}'.");
            }

            job.Status = AgentJobStatus.Completed;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.Error = null;
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

    private sealed record JobCompatibilityPayload(Guid? RunId);
}
