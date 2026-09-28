using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

/// <summary>
/// Single source of truth for CampaignContainer lifecycle and synchronization.
/// Jobs, pack provisioning and campaign launch should not mutate container state directly.
/// </summary>
public interface ICampaignContainerRuntime
{
    Task<CampaignContainer> EnsureAsync(
        Guid tenantId,
        Guid campaignId,
        Guid agentId,
        string name,
        string packageCode,
        string packageVersion,
        string configurationJson,
        CancellationToken ct = default);

    void Queue(CampaignContainer container);
    void Start(CampaignContainer container, DateTime nowUtc);
    void Fail(CampaignContainer container, DateTime nowUtc);
    void Complete(CampaignContainer container, DateTime nowUtc);
    void Stop(CampaignContainer container, DateTime nowUtc);
}

public sealed class CampaignContainerRuntime(AppDbContext db) : ICampaignContainerRuntime
{
    public async Task<CampaignContainer> EnsureAsync(
        Guid tenantId,
        Guid campaignId,
        Guid agentId,
        string name,
        string packageCode,
        string packageVersion,
        string configurationJson,
        CancellationToken ct = default)
    {
        var container = await db.CampaignContainers
            .SingleOrDefaultAsync(x =>
                x.TenantId == tenantId &&
                x.CampaignId == campaignId &&
                x.AgentId == agentId, ct);

        if (container is null)
        {
            container = new CampaignContainer
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CampaignId = campaignId,
                AgentId = agentId,
                Name = name,
                PackageCode = packageCode,
                PackageVersion = packageVersion,
                ConfigurationJson = string.IsNullOrWhiteSpace(configurationJson) ? "{}" : configurationJson,
                Status = CampaignContainerStatus.Queued
            };

            db.CampaignContainers.Add(container);
            return container;
        }

        container.Name = name;
        container.PackageCode = packageCode;
        container.PackageVersion = packageVersion;
        container.ConfigurationJson = string.IsNullOrWhiteSpace(configurationJson) ? "{}" : configurationJson;

        if (container.Status is CampaignContainerStatus.Stopped or
            CampaignContainerStatus.Failed or
            CampaignContainerStatus.Pending)
        {
            Queue(container);
        }

        container.UpdatedAtUtc = DateTime.UtcNow;
        return container;
    }

    public void Queue(CampaignContainer container)
    {
        if (container.Status == CampaignContainerStatus.Running)
            return;

        container.Status = CampaignContainerStatus.Queued;
        container.LastStoppedAtUtc = null;
        container.CompletedAtUtc = null;
        container.UpdatedAtUtc = DateTime.UtcNow;
    }

    public void Start(CampaignContainer container, DateTime nowUtc)
    {
        container.Status = CampaignContainerStatus.Starting;
        container.LastStartedAtUtc ??= nowUtc;
        container.LastStoppedAtUtc = null;
        container.CompletedAtUtc = null;
        container.UpdatedAtUtc = nowUtc;
    }

    public void Fail(CampaignContainer container, DateTime nowUtc)
    {
        container.Status = CampaignContainerStatus.Failed;
        container.LastStoppedAtUtc = nowUtc;
        container.CompletedAtUtc = null;
        container.UpdatedAtUtc = nowUtc;
    }

    public void Complete(CampaignContainer container, DateTime nowUtc)
    {
        container.Status = CampaignContainerStatus.Completed;
        container.CompletedAtUtc = nowUtc;
        container.LastStoppedAtUtc = null;
        container.UpdatedAtUtc = nowUtc;
    }

    public void Stop(CampaignContainer container, DateTime nowUtc)
    {
        container.Status = CampaignContainerStatus.Stopped;
        container.LastStoppedAtUtc = nowUtc;
        container.UpdatedAtUtc = nowUtc;
    }
}
