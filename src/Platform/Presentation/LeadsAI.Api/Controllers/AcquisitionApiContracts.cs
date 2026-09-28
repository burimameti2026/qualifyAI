public sealed record DiscoveryRequest(
    string? Source = null,
    string? Region = null,
    int MaximumResults = 50,
    int MinimumScore = 70,
    string? TargetListName = null,
    bool CreateTargetList = true);

public sealed record IcpSaveRequest(
    Guid? Id,
    string Name,
    string? Industry,
    string? CountriesCsv,
    int? MinimumEmployees,
    int? MaximumEmployees,
    string? IntentKeywordsCsv,
    string? CriteriaJson,
    bool Active = true,
    int MinimumScore = 70);

public sealed record CampaignContainerCreateRequest(string? Name, string? PackageCode, string? PackageVersion, string? ConfigurationJson);
public sealed record ContainerTargetListRequest(Guid? TargetListId);
public sealed record CampaignPlanRequest(string PlanJson);
public sealed record CampaignMessagesRequest(IReadOnlyList<CampaignMessageStepRequest> Steps);
public sealed record CampaignMessageStepRequest(int StepNumber, int DelayHours, string Channel, string SubjectTemplate, string BodyTemplate);
public sealed record DeliveryConfirmation(string ProviderMessageId);
public sealed record ReplyInput(Guid TenantId, Guid CampaignId, Guid ProspectId, Guid? OutreachMessageId, string Body, string Classification, int SentimentScore, bool RequiresHuman);
public sealed record CampaignActivityItem(Guid Id, DateTime AtUtc, string Type, string Status, string Title, string Detail);
