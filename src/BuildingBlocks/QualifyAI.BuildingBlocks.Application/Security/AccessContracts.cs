namespace QualifyAI.BuildingBlocks.Application.Security;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class AccessControlAttribute(string permission, string module) : Attribute
{
    public string Permission { get; } = permission;
    public string Module { get; } = module;
}

public sealed record TenantAccessSnapshot(
    Guid TenantId,
    bool IsAccessible,
    long Version,
    IReadOnlyCollection<string> Modules)
{
    public bool HasModule(string module)
        => Modules.Any(x => string.Equals(x, module, StringComparison.OrdinalIgnoreCase));
}

public interface IRequestSecurityContext
{
    bool IsAuthenticated { get; }
    Guid? TenantId { get; }
    long? LicenseVersion { get; }
    bool HasPermission(string permission);
    Task<TenantAccessSnapshot?> GetEntitlementAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

public sealed class RequestAccessDeniedException(string reason) : UnauthorizedAccessException(reason)
{
    public string Reason { get; } = reason;
}
