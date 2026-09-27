using QualifyAI.Identity.Domain.AccessControl;

namespace QualifyAI.Identity.Application.AccessControl;

public sealed record AccessRoleDto(
    Guid Id,
    Guid? TenantId,
    string Name,
    string Description,
    AccessRoleScope Scope,
    bool IsSystem,
    bool IsActive,
    IReadOnlyList<string> Permissions);

public sealed record PermissionDefinitionDto(
    string Code,
    string Module,
    string DisplayName,
    string Description,
    bool PlatformOnly);

public sealed record SecurityAuditDto(
    Guid Id,
    Guid? TenantId,
    Guid? ActorUserId,
    string Action,
    string TargetType,
    string TargetId,
    string DetailsJson,
    DateTime OccurredAtUtc);

public interface IAccessControlRepository
{
    Task<IReadOnlyList<AccessRoleDto>> ListRolesAsync(Guid? tenantId, bool includePlatform, CancellationToken ct = default);
    Task<AccessRoleDto?> GetRoleAsync(Guid roleId, CancellationToken ct = default);
    Task<AccessRoleDto> CreateRoleAsync(Guid? tenantId, string name, string description, AccessRoleScope scope, bool isSystem, Guid? actorUserId, CancellationToken ct = default);
    Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissions, Guid? actorUserId, CancellationToken ct = default);
    Task SetClientPermissionsAsync(Guid clientApplicationId, IReadOnlyCollection<string> permissions, Guid? actorUserId, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetClientPermissionsAsync(Guid clientApplicationId, CancellationToken ct = default);
    Task<IReadOnlyList<PermissionDefinitionDto>> ListPermissionsAsync(CancellationToken ct = default);
    Task EnsurePermissionCatalogAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SecurityAuditDto>> ListAuditAsync(Guid? tenantId, int take, CancellationToken ct = default);
    Task<AccessRole?> FindTenantRoleAsync(Guid tenantId, string displayName, CancellationToken ct = default);
    Task EnsureTenantRoleAsync(Guid roleId, Guid tenantId, string displayName, string description, CancellationToken ct = default);
    Task<IReadOnlyList<string>> ResolveUserPermissionsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
