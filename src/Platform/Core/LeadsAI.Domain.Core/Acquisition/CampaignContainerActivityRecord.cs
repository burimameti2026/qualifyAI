namespace LeadsAI.Domain;

/// <summary>
/// Durable, tenant-scoped operational history for a campaign container.
/// Kept separate from AuditLog, which is reserved for administrative/security events.
/// </summary>
public sealed class CampaignContainerActivityRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid ContainerId { get; set; }
    public Guid? RunId { get; set; }
    public Guid? TaskId { get; set; }
    public int ContainerVersion { get; set; }
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string Level { get; set; } = "info";
    public string EventType { get; set; } = string.Empty;
    public string StepType { get; set; } = string.Empty;
    public string StepName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string DataJson { get; set; } = "{}";
}
