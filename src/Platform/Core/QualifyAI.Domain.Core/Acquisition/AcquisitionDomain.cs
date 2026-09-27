namespace QualifyAI.Domain;

public enum ProspectStatus { Discovered, Enriched, Qualified, Nurturing, Replied, DemoReady, Converted, Suppressed }
public enum CampaignStatus { Draft, Scheduled, Running, Paused, Completed }
public enum OutreachStatus { Queued, Sent, Delivered, Replied, Failed, Suppressed }

public sealed class Prospect : TenantEntity
{
    public Guid? CompanyId { get; set; }
    public Guid? ContactId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Source { get; set; } = "manual";
    public string Priority { get; set; } = string.Empty;
    public string ContactReadiness { get; set; } = string.Empty;
    public string SuggestedBuyer { get; set; } = string.Empty;
    public string SizeBand { get; set; } = string.Empty;
    public string PainHypothesis { get; set; } = string.Empty;
    public string Offer { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string VerificationStatus { get; set; } = string.Empty;
    public string OutreachStatus { get; set; } = string.Empty;
    public string DatasetOrigin { get; set; } = string.Empty;
    public int FitScore { get; set; }
    public int IntentScore { get; set; }
    public ProspectStatus Status { get; set; } = ProspectStatus.Discovered;
    public DateTime? LastEvaluatedAtUtc { get; set; }

    public int PriorityScore => (int)Math.Round(FitScore * .55m + IntentScore * .45m);

    public void Evaluate(int fitScore, int intentScore)
    {
        FitScore = Math.Clamp(fitScore, 0, 100);
        IntentScore = Math.Clamp(intentScore, 0, 100);
        Status = PriorityScore >= 75 ? ProspectStatus.Qualified : ProspectStatus.Enriched;
        LastEvaluatedAtUtc = DateTime.UtcNow;
        Touch();
    }
}

public sealed class ProspectSignal : TenantEntity
{
    public Guid ProspectId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Evidence { get; set; } = string.Empty;
    public int Score { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime ObservedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class TargetList : TenantEntity
{
    public string Name { get; set; } = string.Empty;
    public Guid? IcpProfileId { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool Dynamic { get; set; }
}

public sealed class TargetListMember : TenantEntity
{
    public Guid TargetListId { get; set; }
    public Guid ProspectId { get; set; }
    public DateTime AddedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class Campaign : TenantEntity
{
    public Guid TargetListId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Goal { get; set; } = "book-demo";
    public CampaignStatus Status { get; set; } = CampaignStatus.Draft;
    public string SenderName { get; set; } = string.Empty;
    public string SenderEmail { get; set; } = string.Empty;
    public DateTime? StartsAtUtc { get; set; }

    public void Start()
    {
        if (Status is CampaignStatus.Completed) throw new InvalidOperationException("Completed campaigns cannot be restarted.");
        Status = CampaignStatus.Running;
        StartsAtUtc ??= DateTime.UtcNow;
        Touch();
    }
}

public sealed class CampaignStep : TenantEntity
{
    public Guid CampaignId { get; set; }
    public int StepNumber { get; set; }
    public int DelayHours { get; set; }
    public string Channel { get; set; } = "email";
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
}

public sealed class CampaignRecipient : TenantEntity
{
    public Guid CampaignId { get; set; }
    public Guid ProspectId { get; set; }
    public int CurrentStep { get; set; }
    public string Status { get; set; } = "active";
    public DateTime? NextRunAtUtc { get; set; }
    public DateTime? RepliedAtUtc { get; set; }
}

public sealed class OutreachMessage : TenantEntity
{
    public Guid CampaignId { get; set; }
    public Guid ProspectId { get; set; }
    public Guid CampaignStepId { get; set; }
    public string Channel { get; set; } = "email";
    public string Direction { get; set; } = "outbound";
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public OutreachStatus Status { get; set; } = OutreachStatus.Queued;
    public string ProviderMessageId { get; set; } = string.Empty;
    public DateTime? SentAtUtc { get; set; }
}

public sealed class ProspectReply : TenantEntity
{
    public Guid CampaignId { get; set; }
    public Guid ProspectId { get; set; }
    public Guid? OutreachMessageId { get; set; }
    public string Body { get; set; } = string.Empty;
    public string Classification { get; set; } = "unclassified";
    public int SentimentScore { get; set; }
    public bool RequiresHuman { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
}
