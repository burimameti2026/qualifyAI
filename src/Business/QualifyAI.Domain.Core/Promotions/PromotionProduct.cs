namespace QualifyAI.Domain.Core.Promotions;

public sealed class PromotionProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? KeyBenefits { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
}
