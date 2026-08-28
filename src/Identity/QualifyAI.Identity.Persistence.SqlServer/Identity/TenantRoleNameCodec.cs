namespace QualifyAI.Identity.Persistence.SqlServer.Identity;

public static class TenantRoleNameCodec
{
    public static string ToStorageName(Guid tenantId, string displayName)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(displayName)) throw new ArgumentException("Role name is required.", nameof(displayName));

        return $"{tenantId:N}:{displayName.Trim()}";
    }

    public static string ToDisplayName(Guid tenantId, string storageName)
    {
        if (string.IsNullOrWhiteSpace(storageName)) return string.Empty;

        var prefix = $"{tenantId:N}:";
        return storageName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? storageName[prefix.Length..]
            : storageName;
    }
}
