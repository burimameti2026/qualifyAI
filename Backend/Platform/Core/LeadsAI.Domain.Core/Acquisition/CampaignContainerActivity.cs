namespace LeadsAI.Domain;

public sealed record CampaignContainerActivity(
    Guid Id,
    Guid TenantId,
    Guid ContainerId,
    Guid RunId,
    Guid? TaskId,
    int ContainerVersion,
    DateTime AtUtc,
    string Level,
    string EventType,
    string StepType,
    string StepName,
    string Message,
    object? Data);