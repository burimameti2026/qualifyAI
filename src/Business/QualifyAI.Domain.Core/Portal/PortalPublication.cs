namespace QualifyAI.Domain.Core.Portal;

public sealed class PortalPublication
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid CatalogProductId { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Status { get; set; } = "Draft";
    public bool IsVisible { get; set; }
    public int Version { get; set; } = 1;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
