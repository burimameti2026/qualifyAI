namespace LeadsAI.Domain;

public enum CampaignContainerStatus { Stopped, Running, Paused, Failed, Pending, Queued, Starting, Completed }

public sealed class CampaignContainer : TenantEntity
{
    public Guid CampaignId { get; set; }
    public Guid AgentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PackageCode { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;

    // Runtime version snapshot. Each version owns its package, plan and task configuration.
    public int Version { get; set; } = 1;
    public string VersionLabel { get; set; } = "v1";
    public string ChangeSummary { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = "{}";
    public string ChangesJson { get; set; } = "[]";

    public CampaignContainerStatus Status { get; set; } = CampaignContainerStatus.Queued;
    public DateTime? LastStartedAtUtc { get; set; }
    public DateTime? LastStoppedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}