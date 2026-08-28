using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.Identity.Application.AccessControl;
using QualifyAI.Identity.Domain.AccessControl;
using QualifyAI.Identity.Persistence.SqlServer.Identity;

namespace QualifyAI.Identity.Persistence.SqlServer.Repositories;

public sealed class AccessControlRepository(IdentityDbContext db) : IAccessControlRepository
{
    public async Task<IReadOnlyList<AccessRoleDto>> ListRolesAsync(Guid? tenantId, bool includePlatform, CancellationToken ct = default)
    {
        var query = db.AccessRoles.AsNoTracking();
        query = includePlatform
            ? query.Where(x => x.TenantId == tenantId || x.Scope == AccessRoleScope.Platform)
            : query.Where(x => x.TenantId == tenantId);

        var roles = await query.OrderBy(x => x.Scope).ThenBy(x => x.Name).ToListAsync(ct);
        var roleIds = roles.Select(x => x.Id).ToArray();
        var grants = await db.RolePermissionGrants.AsNoTracking()
            .Where(x => roleIds.Contains(x.RoleId))
            .ToListAsync(ct);
        var byRole = grants.GroupBy(x => x.RoleId)
            .ToDictionary(x => x.Key, x => (IReadOnlyList<string>)x.Select(y => y.Permission).OrderBy(y => y).ToList());

        return roles.Select(x => Map(x, byRole.GetValueOrDefault(x.Id) ?? Array.Empty<string>())).ToList();
    }

    public async Task<AccessRoleDto?> GetRoleAsync(Guid roleId, CancellationToken ct = default)
    {
        var role = await db.AccessRoles.AsNoTracking().FirstOrDefaultAsync(x => x.Id == roleId, ct);
        if (role is null) return null;
        var permissions = await db.RolePermissionGrants.AsNoTracking()
            .Where(x => x.RoleId == roleId).Select(x => x.Permission).OrderBy(x => x).ToListAsync(ct);
        return Map(role, permissions);
    }

    public async Task<AccessRoleDto> CreateRoleAsync(Guid? tenantId, string name, string description, AccessRoleScope scope, bool isSystem, Guid? actorUserId, CancellationToken ct = default)
    {
        if (scope == AccessRoleScope.Tenant && !tenantId.HasValue) throw new InvalidOperationException("Tenant roles require TenantId.");
        if (scope == AccessRoleScope.Platform && tenantId.HasValue) throw new InvalidOperationException("Platform roles cannot belong to a tenant.");

        var normalized = name.Trim().ToUpperInvariant();
        if (await db.AccessRoles.AnyAsync(x => x.TenantId == tenantId && x.NormalizedName == normalized, ct))
            throw new InvalidOperationException("Role already exists.");

        var role = scope == AccessRoleScope.Platform
            ? AccessRole.CreatePlatform(name, description, isSystem)
            : AccessRole.CreateTenant(tenantId!.Value, name, description, isSystem);

        db.AccessRoles.Add(role);
        var storageName = scope == AccessRoleScope.Platform
            ? $"platform:{role.Name}"
            : TenantRoleNameCodec.ToStorageName(tenantId!.Value, role.Name);
        db.Roles.Add(new ApplicationRole
        {
            Id = role.Id,
            TenantId = tenantId ?? Guid.Empty,
            Name = storageName,
            NormalizedName = storageName.ToUpperInvariant(),
            Description = role.Name,
            ConcurrencyStamp = Guid.NewGuid().ToString("N")
        });

        Audit(tenantId, actorUserId, "role.created", "role", role.Id.ToString(), new { role.Name, role.Scope, role.IsSystem });
        await db.SaveChangesAsync(ct);
        return Map(role, Array.Empty<string>());
    }

    public async Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<string> permissions, Guid? actorUserId, CancellationToken ct = default)
    {
        var role = await db.AccessRoles.FirstOrDefaultAsync(x => x.Id == roleId, ct) ?? throw new KeyNotFoundException("Role not found.");
        var normalized = ValidatePermissions(permissions);
        if (role.Scope == AccessRoleScope.Tenant && normalized.Contains(QualifyAiPermissions.SystemAdmin, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("system.admin is platform-only.");

        var existing = await db.RolePermissionGrants.Where(x => x.RoleId == roleId).ToListAsync(ct);
        db.RolePermissionGrants.RemoveRange(existing);
        db.RolePermissionGrants.AddRange(normalized.Select(x => new RolePermissionGrant(roleId, x)));
        Audit(role.TenantId, actorUserId, "role.permissions.changed", "role", roleId.ToString(), new { permissions = normalized });
        await db.SaveChangesAsync(ct);
    }

    public async Task SetClientPermissionsAsync(Guid clientApplicationId, IReadOnlyCollection<string> permissions, Guid? actorUserId, CancellationToken ct = default)
    {
        var client = await db.ClientApplications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == clientApplicationId, ct)
            ?? throw new KeyNotFoundException("Client application not found.");
        var normalized = ValidatePermissions(permissions);
        if (client.TenantId.HasValue && normalized.Contains(QualifyAiPermissions.SystemAdmin, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("Tenant clients cannot receive system.admin.");

        var existing = await db.ClientPermissionGrants.Where(x => x.ClientApplicationId == clientApplicationId).ToListAsync(ct);
        db.ClientPermissionGrants.RemoveRange(existing);
        db.ClientPermissionGrants.AddRange(normalized.Select(x => new ClientPermissionGrant(clientApplicationId, x)));
        Audit(client.TenantId, actorUserId, "client.permissions.changed", "client", clientApplicationId.ToString(), new { permissions = normalized });
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetClientPermissionsAsync(Guid clientApplicationId, CancellationToken ct = default)
        => await db.ClientPermissionGrants.AsNoTracking().Where(x => x.ClientApplicationId == clientApplicationId)
            .Select(x => x.Permission).OrderBy(x => x).ToListAsync(ct);

    public async Task<IReadOnlyList<PermissionDefinitionDto>> ListPermissionsAsync(CancellationToken ct = default)
        => await db.PermissionDefinitions.AsNoTracking().OrderBy(x => x.Module).ThenBy(x => x.Code)
            .Select(x => new PermissionDefinitionDto(x.Code, x.Module, x.DisplayName, x.Description, x.PlatformOnly)).ToListAsync(ct);

    public async Task EnsurePermissionCatalogAsync(CancellationToken ct = default)
    {
        var existingCodes = await db.PermissionDefinitions.AsNoTracking().Select(x => x.Code).ToListAsync(ct);
        var existing = existingCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var permission in Catalog().Where(x => !existing.Contains(x.Code))) db.PermissionDefinitions.Add(permission);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SecurityAuditDto>> ListAuditAsync(Guid? tenantId, int take, CancellationToken ct = default)
        => await db.SecurityAuditEntries.AsNoTracking()
            .Where(x => tenantId == null || x.TenantId == tenantId)
            .OrderByDescending(x => x.OccurredAtUtc).Take(take)
            .Select(x => new SecurityAuditDto(x.Id, x.TenantId, x.ActorUserId, x.Action, x.TargetType, x.TargetId, x.DetailsJson, x.OccurredAtUtc)).ToListAsync(ct);

    public Task<AccessRole?> FindTenantRoleAsync(Guid tenantId, string displayName, CancellationToken ct = default)
    {
        var normalized = displayName.Trim().ToUpperInvariant();
        return db.AccessRoles.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.NormalizedName == normalized, ct);
    }

    public async Task EnsureTenantRoleAsync(Guid roleId, Guid tenantId, string displayName, string description, CancellationToken ct = default)
    {
        if (await db.AccessRoles.AnyAsync(x => x.Id == roleId, ct)) return;
        db.AccessRoles.Add(AccessRole.Rehydrate(roleId, tenantId, displayName, description, AccessRoleScope.Tenant, false));
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<string>> ResolveUserPermissionsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var direct = db.UserPermissions.AsNoTracking().Where(x => x.TenantId == tenantId && x.UserId == userId).Select(x => x.Permission);
        var roleIds = db.Set<IdentityUserRole<Guid>>().AsNoTracking().Where(x => x.UserId == userId).Select(x => x.RoleId);
        var fromRoles = db.RolePermissionGrants.AsNoTracking().Where(x => roleIds.Contains(x.RoleId)).Select(x => x.Permission);
        return await direct.Concat(fromRoles).Distinct().OrderBy(x => x).ToListAsync(ct);
    }

    private static string[] ValidatePermissions(IReadOnlyCollection<string> permissions)
    {
        var normalized = permissions.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim().ToLowerInvariant()).Distinct().ToArray();
        var valid = QualifyAiPermissions.All.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var invalid = normalized.Where(x => !valid.Contains(x)).ToArray();
        if (invalid.Length > 0) throw new InvalidOperationException($"Unknown permissions: {string.Join(", ", invalid)}");
        return normalized;
    }

    private void Audit(Guid? tenantId, Guid? actor, string action, string targetType, string targetId, object details)
        => db.SecurityAuditEntries.Add(new SecurityAuditEntry(tenantId, actor, action, targetType, targetId, JsonSerializer.Serialize(details)));

    private static AccessRoleDto Map(AccessRole role, IReadOnlyList<string> permissions)
        => new(role.Id, role.TenantId, role.Name, role.Description, role.Scope, role.IsSystem, role.IsActive, permissions);

    private static IEnumerable<PermissionDefinition> Catalog()
    {
        static PermissionDefinition P(string code, string module, string name, bool platformOnly = false)
            => new(code, module, name, name, platformOnly);

        yield return P(QualifyAiPermissions.SystemAdmin, QualifyAiModules.Settings, "System administration", true);
        yield return P(QualifyAiPermissions.UsersRead, QualifyAiModules.Settings, "Read users");
        yield return P(QualifyAiPermissions.UsersManage, QualifyAiModules.Settings, "Manage users");
        yield return P(QualifyAiPermissions.CrmRead, QualifyAiModules.Crm, "Read CRM");
        yield return P(QualifyAiPermissions.CrmManage, QualifyAiModules.Crm, "Manage CRM");
        yield return P(QualifyAiPermissions.ConversationsRead, QualifyAiModules.Inbox, "Read conversations");
        yield return P(QualifyAiPermissions.ConversationsManage, QualifyAiModules.Inbox, "Manage conversations");
        yield return P(QualifyAiPermissions.TicketsRead, QualifyAiModules.Ticketing, "Read tickets");
        yield return P(QualifyAiPermissions.TicketsManage, QualifyAiModules.Ticketing, "Manage tickets");
        yield return P(QualifyAiPermissions.KnowledgeRead, QualifyAiModules.Knowledge, "Read knowledge");
        yield return P(QualifyAiPermissions.KnowledgeManage, QualifyAiModules.Knowledge, "Manage knowledge");
        yield return P(QualifyAiPermissions.AgentsRead, QualifyAiModules.Ai, "Read AI agents");
        yield return P(QualifyAiPermissions.AgentsManage, QualifyAiModules.Ai, "Manage AI agents");
        yield return P(QualifyAiPermissions.AutomationRead, QualifyAiModules.Automation, "Read automations");
        yield return P(QualifyAiPermissions.AutomationManage, QualifyAiModules.Automation, "Manage automations");
        yield return P(QualifyAiPermissions.IntegrationsRead, QualifyAiModules.Integrations, "Read integrations");
        yield return P(QualifyAiPermissions.IntegrationsManage, QualifyAiModules.Integrations, "Manage integrations");
        yield return P(QualifyAiPermissions.AnalyticsRead, QualifyAiModules.Analytics, "Read analytics");
        yield return P(QualifyAiPermissions.BillingRead, QualifyAiModules.Billing, "Read billing");
        yield return P(QualifyAiPermissions.BillingManage, QualifyAiModules.Billing, "Manage billing");
        yield return P(QualifyAiPermissions.AuditRead, QualifyAiModules.Settings, "Read audit");
        yield return P(QualifyAiPermissions.SettingsManage, QualifyAiModules.Settings, "Manage settings");
    }
}
