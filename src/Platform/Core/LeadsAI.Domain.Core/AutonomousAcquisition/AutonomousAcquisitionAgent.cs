namespace LeadsAI.Domain;

public enum AutonomousAgentStatus
{
    Draft,
    Active,
    Paused,
    Stopped,
    Failed
}

public enum AutonomousAgentRunStatus
{
    Queued,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum AutonomousAgentTaskStatus
{
    Pending,
    Running,
    Completed,
    Paused,
    Failed,
    Skipped
}

public static class AutonomousAgentTaskTypes
{
    public const string Discover = "discover";
    public const string Qualify = "qualify";
    public const string Enrich = "enrich";
    public const string BuildTargetList = "target-list";
    public const string Outreach = "outreach";
}

public sealed class AutonomousAcquisitionTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public int Sequence { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AutonomousAgentTaskStatus Status { get; set; } = AutonomousAgentTaskStatus.Pending;
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

public sealed class AutonomousAcquisitionAgent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = "custom";
    public string Industry { get; set; } = string.Empty;
    public string Region { get; set; } = "Europe";
    public string CountriesJson { get; set; } = "[]";
    public string IcpJson { get; set; } = "{}";
    public int MinimumScore { get; set; } = 90;
    public int DailyDiscoveryLimit { get; set; } = 50;
    public int DailyEmailLimit { get; set; } = 10;
    public TimeOnly RunTimeUtc { get; set; } = new(8, 0);
    public AutonomousAgentStatus Status { get; set; } = AutonomousAgentStatus.Draft;
    public DateTime? LastRunAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AutonomousAcquisitionAgentRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid AgentId { get; set; }
    public AutonomousAgentRunStatus Status { get; set; } = AutonomousAgentRunStatus.Queued;
    public bool IsManual { get; set; }
    public string? Query { get; set; }
    public DateTime ScheduledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int DiscoveredCount { get; set; }
    public int QualifiedCount { get; set; }
    public int HighScoreCount { get; set; }
    public int EmailsQueuedCount { get; set; }
    public int EmailsSentCount { get; set; }
    public string? Error { get; set; }
}
