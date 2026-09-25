namespace LeadsAI.Domain;

public static class AutonomousAgentTaskTypes
{
    public const string Discover = "discover";
    public const string Qualify = "qualify";
    public const string Enrich = "enrich";
    public const string BuildTargetList = "target-list";
    public const string Outreach = "outreach";
}

public enum AutonomousAcquisitionTaskStatus { Pending, Running, Completed, Paused, Failed, Skipped }

public sealed class AutonomousAcquisitionTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public int Sequence { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AutonomousAcquisitionTaskStatus Status { get; set; } = AutonomousAcquisitionTaskStatus.Pending;
    public bool RequiresApproval { get; set; }
    public string ConfigurationJson { get; set; } = "{}";
    public string ResultJson { get; set; } = "{}";
    public string? Error { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}