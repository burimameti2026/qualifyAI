namespace LeadsAI.Domain;

public enum CampaignContainerStatus { Stopped, Running, Paused, Failed }

public sealed class CampaignContainer : TenantEntity
{
    public Guid CampaignId { get; set; }
    public Guid AgentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PackageCode { get; set; } = string.Empty;
    public string PackageVersion { get; set; } = string.Empty;
    public CampaignContainerStatus Status { get; set; } = CampaignContainerStatus.Stopped;
    public string ConfigurationJson { get; set; } = "{}";
    public DateTime? LastStartedAtUtc { get; set; }
    public DateTime? LastStoppedAtUtc { get; set; }
}
