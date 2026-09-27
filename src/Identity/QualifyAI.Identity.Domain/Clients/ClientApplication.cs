using QualifyAI.BuildingBlocks.Domain.Abstractions;

namespace QualifyAI.Identity.Domain.Clients;

public sealed class ClientApplication : AggregateRoot
{
    private readonly List<ClientScope> _scopes = [];

    private ClientApplication() { }

    private ClientApplication(Guid? tenantId, string clientId, string displayName)
    {
        TenantId = tenantId;
        ClientId = Require(clientId, nameof(clientId)).ToLowerInvariant();
        DisplayName = Require(displayName, nameof(displayName));
        Status = ClientApplicationStatus.Active;
        CreatedAtUtc = DateTime.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public Guid? TenantId { get; private set; }
    public string ClientId { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public ClientApplicationStatus Status { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }
    public IReadOnlyCollection<ClientScope> Scopes => _scopes.AsReadOnly();

    public static ClientApplication Create(
        Guid? tenantId,
        string clientId,
        string displayName,
        IEnumerable<string> scopes)
    {
        var client = new ClientApplication(tenantId, clientId, displayName);
        client.ReplaceScopes(scopes);
        return client;
    }

    public void Rename(string displayName)
    {
        DisplayName = Require(displayName, nameof(displayName));
        Touch();
    }

    public void ReplaceScopes(IEnumerable<string> scopes)
    {
        var normalized = scopes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim().ToLowerInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _scopes.RemoveAll(x => !normalized.Contains(x.Name, StringComparer.OrdinalIgnoreCase));
        foreach (var scope in normalized.Where(x => _scopes.All(s => !string.Equals(s.Name, x, StringComparison.OrdinalIgnoreCase))))
            _scopes.Add(ClientScope.Create(Id, scope));

        Touch();
    }

    public void Disable()
    {
        Status = ClientApplicationStatus.Disabled;
        Touch();
    }

    public void Enable()
    {
        Status = ClientApplicationStatus.Active;
        Touch();
    }

    private void Touch() => UpdatedAtUtc = DateTime.UtcNow;

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("Value is required.", name)
            : value.Trim();
}

public enum ClientApplicationStatus
{
    Active = 1,
    Disabled = 2
}
