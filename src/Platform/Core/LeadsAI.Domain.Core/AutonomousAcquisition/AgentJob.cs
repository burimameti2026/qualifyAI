namespace LeadsAI.Domain.Core;

public enum AgentJobStatus
{
    Queued,
    Running,
    Waiting,
    Completed,
    Failed,
    Cancelled
}

/// <summary>
/// The single persisted unit of autonomous execution.
/// A workflow describes what should happen; a Job is the concrete work item
/// claimed by one tenant worker and executed by that tenant's agent.
/// </summary>
public sealed class AgentJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CampaignId { get; set; }
    public Guid? ContainerId { get; set; }
    public int? ContainerVersion { get; set; }
    public Guid AgentId { get; set; }

    public string Type { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public Guid? TaskId { get; set; }
    public string TaskPayloadJson { get; set; } = "{}";
    public bool IsManual { get; set; }
    public string? Query { get; set; }
    public int DiscoveredCount { get; set; }
    public int QualifiedCount { get; set; }
    public int HighScoreCount { get; set; }
    public int EmailsQueuedCount { get; set; }
    public int EmailsSentCount { get; set; }
    public int Sequence { get; set; }
    public AgentJobStatus Status { get; set; } = AgentJobStatus.Queued;

    public string PayloadJson { get; set; } = "{}";
    public string? ResultJson { get; set; }
    public string? Error { get; set; }

    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 3;
    public int Priority { get; set; }

    public DateTime ScheduledAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime? LeaseUntilUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }

    public string? WorkerId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
