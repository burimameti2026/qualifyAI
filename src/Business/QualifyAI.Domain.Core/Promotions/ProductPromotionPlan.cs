namespace QualifyAI.Domain.Core.Promotions;

public sealed class ProductPromotionPlan
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PromotionProductId { get; set; }
    public Guid TargetMarketId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CampaignLanguage { get; set; } = "en";
    public string Status { get; set; } = "Draft";
    public string? TargetCustomerProfile { get; set; }
    public string? QualificationRules { get; set; }
    public string? MessagingStrategy { get; set; }
    public bool EnableAutonomousProspecting { get; set; }
    public bool EnableAutomaticCampaignEnrollment { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ActivatedAt { get; set; }
}
