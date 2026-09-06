using QualifyAI.BuildingBlocks.Domain.Abstractions;

namespace QualifyAI.Identity.Domain.Clients;

public sealed class ClientScope : Entity
{
    private ClientScope() { }

    private ClientScope(Guid clientApplicationId, string name)
    {
        ClientApplicationId = clientApplicationId;
        Name = string.IsNullOrWhiteSpace(name)
            ? throw new ArgumentException("Scope is required.", nameof(name))
            : name.Trim().ToLowerInvariant();
    }

    public Guid ClientApplicationId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    internal static ClientScope Create(Guid clientApplicationId, string name)
        => new(clientApplicationId, name);
}
