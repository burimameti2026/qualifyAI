using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using QualifyAI.Application;

namespace QualifyAI.Infrastructure;

public interface ITenantDatabaseConnectionResolver
{
    string ResolveConnectionString(CurrentTenant? tenant);
    IReadOnlyDictionary<string, string> GetConfiguredTenantDatabases();
}

public sealed class TenantDatabaseConnectionResolver(IConfiguration configuration) : ITenantDatabaseConnectionResolver
{
    private readonly string _defaultConnection = configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is required.");

    private readonly IReadOnlyDictionary<string, string> _tenantDatabases =
        configuration.GetSection("TenantDatabases").GetChildren()
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .ToDictionary(
                x => x.Key.Trim().ToLowerInvariant(),
                x => x.Value!.Trim(),
                StringComparer.OrdinalIgnoreCase);

    public string ResolveConnectionString(CurrentTenant? tenant)
    {
        if (tenant is null || string.IsNullOrWhiteSpace(tenant.Slug))
            return _defaultConnection;

        if (!_tenantDatabases.TryGetValue(tenant.Slug, out var databaseName))
            return _defaultConnection;

        ValidateDatabaseName(databaseName);

        var builder = new SqlConnectionStringBuilder(_defaultConnection)
        {
            InitialCatalog = databaseName
        };

        return builder.ConnectionString;
    }

    public IReadOnlyDictionary<string, string> GetConfiguredTenantDatabases() => _tenantDatabases;

    private static void ValidateDatabaseName(string databaseName)
    {
        if (databaseName.Length is < 1 or > 128)
            throw new InvalidOperationException($"Invalid tenant database name '{databaseName}'.");

        if (databaseName.Any(c => !(char.IsLetterOrDigit(c) || c == '_' || c == '-')))
            throw new InvalidOperationException($"Invalid tenant database name '{databaseName}'.");
    }
}
