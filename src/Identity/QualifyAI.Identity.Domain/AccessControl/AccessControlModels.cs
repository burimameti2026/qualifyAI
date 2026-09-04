namespace QualifyAI.Identity.Domain.AccessControl;

public sealed class PermissionDefinition
{
    private PermissionDefinition() { }

    public PermissionDefinition(string code, string module, string displayName, string description, bool platformOnly = false)
    {
        Code = NormalizeCode(code);
        Module = NormalizeCode(module);
        DisplayName = Require(displayName, 150);
        Description = (description ?? string.Empty).Trim();
        PlatformOnly = platformOnly;
    }

    public string Code { get; private set; } = string.Empty;
    public string Module { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public bool PlatformOnly { get; private set; }

    private static string NormalizeCode(string value)
    {
        var normalized = Require(value, 200).ToLowerInvariant();
        if (normalized.Any(char.IsWhiteSpace)) throw new ArgumentException("Permission/module codes cannot contain whitespace.", nameof(value));
        return normalized;
    }

    private static string Require(string value, int max)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized.Length > max) throw new ArgumentOutOfRangeException(nameof(value));
        return normalized;
    }
}

public sealed class RolePermissionGrant
{
    private RolePermissionGrant() { }
    public RolePermissionGrant(Guid roleId, string permission)
    {
        Id = Guid.NewGuid();
        RoleId = roleId;
        Permission = permission.Trim().ToLowerInvariant();
        CreatedAtUtc = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid RoleId { get; private set; }
    public string Permission { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
}

public sealed class ClientPermissionGrant
{
    private ClientPermissionGrant() { }
    public ClientPermissionGrant(Guid clientApplicationId, string permission)
    {
        Id = Guid.NewGuid();
        ClientApplicationId = clientApplicationId;
        Permission = permission.Trim().ToLowerInvariant();
        CreatedAtUtc = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid ClientApplicationId { get; private set; }
    public string Permission { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
}

public sealed class SecurityAuditEntry
{
    private SecurityAuditEntry() { }
    public SecurityAuditEntry(Guid? tenantId, Guid? actorUserId, string action, string targetType, string targetId, string detailsJson)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        ActorUserId = actorUserId;
        Action = action.Trim();
        TargetType = targetType.Trim();
        TargetId = targetId.Trim();
        DetailsJson = string.IsNullOrWhiteSpace(detailsJson) ? "{}" : detailsJson;
        OccurredAtUtc = DateTime.UtcNow;
    }
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string TargetType { get; private set; } = string.Empty;
    public string TargetId { get; private set; } = string.Empty;
    public string DetailsJson { get; private set; } = "{}";
    public DateTime OccurredAtUtc { get; private set; }
}
