using System.Data;
using LeadsAI.Domain;
using LeadsAI.Application;
using LeadsAI.Infrastructure.Acquisition;
using LeadsAI.Persistence.SqlServer;
using Microsoft.EntityFrameworkCore;

namespace LeadsAI.Api.Services;

public sealed class CampaignRuntimeService(
    AppDbContext db,
    IAgentJobFactory jobFactory,
    ICampaignContainerRuntime containers)
{
    public async Task<CampaignRuntimeResult?> StartAsync(Guid tenantId, Guid campaignId, CancellationToken ct)
        => await RunLifecycleAsync(tenantId, campaignId, resume: false, ct);

    public async Task<CampaignRuntimeResult?> ResumeAsync(Guid tenantId, Guid campaignId, CancellationToken ct)
        => await RunLifecycleAsync(tenantId, campaignId, resume: true, ct);

    public async Task<CampaignStatus?> PauseAsync(Guid tenantId, Guid campaignId, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == campaignId, ct);
        if (campaign is null) return null;
        campaign.Status = CampaignStatus.Paused;
        if (campaign.AgentId.HasValue)
        {
            var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value, ct);
            if (agent is not null)
            {
                agent.Status = AutonomousAgentStatus.Paused;
                agent.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
        return campaign.Status;
    }

    public async Task<CampaignStatus?> StopAsync(Guid tenantId, Guid campaignId, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == campaignId, ct);
        if (campaign is null) return null;
        campaign.Status = CampaignStatus.Stopped;
        if (campaign.AgentId.HasValue)
        {
            var agent = await db.AutonomousAcquisitionAgents.FirstOrDefaultAsync(
                x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value, ct);
            if (agent is not null)
            {
                agent.Status = AutonomousAgentStatus.Stopped;
                agent.UpdatedAtUtc = DateTime.UtcNow;
            }
        }
        await db.SaveChangesAsync(ct);
        return campaign.Status;
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid campaignId, CancellationToken ct)
    {
        var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == campaignId, ct);
        if (campaign is null) return false;

        var targetListId = campaign.TargetListId;
        var agentId = campaign.AgentId;
        var runIds = await db.AgentJobs.Where(x => x.TenantId == tenantId && x.CampaignId == campaignId).Select(x => x.Id).ToListAsync(ct);
        var messageIds = await db.OutreachMessages.Where(x => x.TenantId == tenantId && x.CampaignId == campaignId).Select(x => x.Id).ToListAsync(ct);
        var approvalTitles = messageIds.Select(x => $"APPROVAL: Send outreach {x}").ToList();

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            if (approvalTitles.Count > 0)
                await db.CrmTasks.Where(x => x.TenantId == tenantId && approvalTitles.Contains(x.Title)).ExecuteDeleteAsync(ct);
            if (messageIds.Count > 0)
                await db.OutreachMessages.Where(x => x.TenantId == tenantId && messageIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
            if (runIds.Count > 0)
            {
                await db.AutonomousAcquisitionTasks.Where(x => x.TenantId == tenantId && x.RunId.HasValue && runIds.Contains(x.RunId.Value)).ExecuteDeleteAsync(ct);
                await db.AgentJobs.Where(x => x.TenantId == tenantId && runIds.Contains(x.Id)).ExecuteDeleteAsync(ct);
            }
            await db.CampaignRecipients.Where(x => x.TenantId == tenantId && x.CampaignId == campaignId).ExecuteDeleteAsync(ct);
            await db.CampaignSteps.Where(x => x.TenantId == tenantId && x.CampaignId == campaignId).ExecuteDeleteAsync(ct);
            await db.TargetListMembers.Where(x => x.TenantId == tenantId && x.TargetListId == targetListId).ExecuteDeleteAsync(ct);
            await db.Campaigns.Where(x => x.TenantId == tenantId && x.Id == campaignId).ExecuteDeleteAsync(ct);
            if (agentId.HasValue)
                await db.AutonomousAcquisitionAgents.Where(x => x.TenantId == tenantId && x.Id == agentId.Value).ExecuteDeleteAsync(ct);
            await db.TargetLists.Where(x => x.TenantId == tenantId && x.Id == targetListId).ExecuteDeleteAsync(ct);
            await transaction.CommitAsync(ct);
        });
        return true;
    }

    private async Task<CampaignRuntimeResult?> RunLifecycleAsync(Guid tenantId, Guid campaignId, bool resume, CancellationToken ct)
    {
        Guid? jobId = null;
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            var campaign = await db.Campaigns.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == campaignId, ct)
                ?? throw new KeyNotFoundException($"Campaign '{campaignId}' was not found.");
            if (resume) campaign.Resume(); else campaign.Start();
            if (!campaign.AgentId.HasValue)
                throw new InvalidOperationException("Campaign agent is not configured.");

            var agent = await db.AutonomousAcquisitionAgents
                .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == campaign.AgentId.Value, ct)
                ?? throw new InvalidOperationException("Campaign agent was not found.");

            if (agent.Status == AutonomousAgentStatus.Stopped)
                throw new InvalidOperationException("A stopped acquisition agent cannot be restarted. Create or reactivate the agent first.");

            agent.Status = AutonomousAgentStatus.Active;
            agent.UpdatedAtUtc = DateTime.UtcNow;

            var container = await containers.EnsureAsync(
                tenantId,
                campaign.Id,
                agent.Id,
                $"{campaign.Name} Container",
                campaign.PackageCode,
                campaign.PackageVersion,
                campaign.PlanJson,
                ct);

            var job = await jobFactory.QueueCampaignAsync(
                tenantId, campaign.Id, agent.Id, container.Id,
                "campaign.execute", $"campaign:{campaign.Id}", true, ct);

            // Resume a paused job in-place instead of creating a second runtime job.
            // A job blocked on outreach approval remains Waiting; the outreach approval
            // workflow owns that gate and must not be bypassed by Resume.
            if (resume && job.Status == AgentJobStatus.Waiting)
            {
                var approvalPending = await db.OutreachMessages.AnyAsync(message =>
                    message.TenantId == tenantId &&
                    message.CampaignId == campaign.Id &&
                    message.Status == OutreachStatus.Queued &&
                    db.CrmTasks.Any(task =>
                        task.TenantId == tenantId &&
                        task.Title == $"APPROVAL: Send outreach {message.Id}" &&
                        !task.Completed), ct);

                if (!approvalPending)
                {
                    job.Status = AgentJobStatus.Queued;
                    job.Error = null;
                    job.LeaseUntilUtc = null;
                    job.WorkerId = null;
                    job.CompletedAtUtc = null;
                    job.ScheduledAtUtc = DateTime.UtcNow;
                    job.UpdatedAtUtc = DateTime.UtcNow;
                    containers.Queue(container);
                }
            }

            jobId = job.Id;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        });
        return new CampaignRuntimeResult(campaignId, CampaignStatus.Running, jobId!.Value);
    }
}

public sealed record CampaignRuntimeResult(Guid CampaignId, CampaignStatus Status, Guid JobId);
