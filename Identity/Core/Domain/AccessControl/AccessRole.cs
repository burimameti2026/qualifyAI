namespace QualifyAI.Identity.Domain.AccessControl;

public enum AccessRoleScope
{
    Tenant = 1,
    Platform = 2
}

public sealed class AccessRole
{
    private AccessRole() { }

    private AccessRole(Guid id, Guid? tenantId, string name, string description, AccessRoleScope scope, bool isSystem)
    {
        Id = id;
        TenantId = tenantId;
        Name = NormalizeDisplayName(name);
        NormalizedName = Name.ToUpperInvariant();
        Description = description.Trim();
        Scope = scope;
        IsSystem = isSystem;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public AccessRoleScope Scope { get; private set; }
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static AccessRole CreateTenant(Guid tenantId, string name, string description = "", bool isSystem = false)
        => new(Guid.NewGuid(), tenantId, name, description, AccessRoleScope.Tenant, isSystem);

    public static AccessRole CreatePlatform(string name, string description = "", bool isSystem = false)
        => new(Guid.NewGuid(), null, name, description, AccessRoleScope.Platform, isSystem);

    public static AccessRole Rehydrate(Guid id, Guid? tenantId, string name, string description, AccessRoleScope scope, bool isSystem)
        => new(id, tenantId, name, description, scope, isSystem);

    public void Rename(string name, string description)
    {
        if (IsSystem) throw new InvalidOperationException("System roles cannot be renamed.");
        Name = NormalizeDisplayName(name);
        NormalizedName = Name.ToUpperInvariant();
        Description = description.Trim();
        UpdatedAtUtc = DateTime.UtcNow;
    }

    public void SetActive(bool active)
    {
        if (IsSystem && !active) throw new InvalidOperationException("System roles cannot be disabled.");
        IsActive = active;
        UpdatedAtUtc = DateTime.UtcNow;
    }

    private static string NormalizeDisplayName(string name)
    {
        var value = name?.Trim() ?? string.Empty;
        if (value.Length < 2 || value.Length > 100) throw new ArgumentOutOfRangeException(nameof(name));
        return value;
    }
}
