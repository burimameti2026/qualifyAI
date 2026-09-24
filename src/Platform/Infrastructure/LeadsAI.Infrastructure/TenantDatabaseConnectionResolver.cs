using Microsoft.Extensions.Configuration;
using LeadsAI.Application;

namespace LeadsAI.Infrastructure;

public interface ITenantDatabaseConnectionResolver
{
    string ResolveConnectionString(CurrentTenant? tenant);
    IReadOnlyDictionary<string, string> GetConfiguredTenantDatabases();
}

/// <summary>
/// Resolves the business database for the current tenant.
/// The business application uses a shared database selected by DefaultConnection;
/// tenant isolation is enforced by TenantId at the data/domain level.
/// This resolver intentionally does not depend on a TenantDatabases configuration
/// or on a tenant-specific database name.
/// </summary>
public sealed class TenantDatabaseConnectionResolver(IConfiguration configuration) : ITenantDatabaseConnectionResolver
{
    private readonly string _defaultConnection = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

    public string ResolveConnectionString(CurrentTenant? tenant) => _defaultConnection;

    public IReadOnlyDictionary<string, string> GetConfiguredTenantDatabases() =>
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
}
