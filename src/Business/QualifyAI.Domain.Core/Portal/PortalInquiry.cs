namespace QualifyAI.Domain.Core.Portal;

public sealed class PortalInquiry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? CatalogProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Company { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? CountryCode { get; set; }
    public string Language { get; set; } = "en";
    public string? Message { get; set; }
    public string Status { get; set; } = "New";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
