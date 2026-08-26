using QualifyAI.BuildingBlocks.Domain.Abstractions;

namespace QualifyAI.Identity.Domain.Tenants;

public sealed class Tenant : AggregateRoot
{
    private Tenant() { }

    private Tenant(string name, string slug, string contactEmail)
    {
        Name = NormalizeRequired(name, nameof(name));
        Slug = NormalizeSlug(slug);
        ContactEmail = NormalizeRequired(contactEmail, nameof(contactEmail)).ToLowerInvariant();
        Status = TenantStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public string Name { get; private set; } = string.Empty;
    public string Slug { get; private set; } = string.Empty;
    public string ContactEmail { get; private set; } = string.Empty;
    public TenantStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static Tenant Create(string name, string slug, string contactEmail)
        => new(name, slug, contactEmail);

    public void Rename(string name)
    {
        Name = NormalizeRequired(name, nameof(name));
        Touch();
    }

    public void ChangeContactEmail(string email)
    {
        ContactEmail = NormalizeRequired(email, nameof(email)).ToLowerInvariant();
        Touch();
    }

    public void Suspend()
    {
        if (Status == TenantStatus.Suspended) return;
        Status = TenantStatus.Suspended;
        Touch();
    }

    public void Activate()
    {
        if (Status == TenantStatus.Active) return;
        Status = TenantStatus.Active;
        Touch();
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;

    private static string NormalizeRequired(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value is required.", name);
        return value.Trim();
    }

    private static string NormalizeSlug(string value)
    {
        var slug = NormalizeRequired(value, nameof(value)).Trim().ToLowerInvariant();
        if (slug.Any(c => !(char.IsLetterOrDigit(c) || c == '-')))
            throw new ArgumentException("Tenant slug may contain only letters, numbers and dashes.", nameof(value));
        return slug;
    }
}

public enum TenantStatus
{
    Provisioning = 0,
    Active = 1,
    Suspended = 2,
    Closed = 3
}
