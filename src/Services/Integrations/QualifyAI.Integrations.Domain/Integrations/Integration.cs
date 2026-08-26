using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Integrations.Domain.Integrations;
public sealed class Integration : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    private Integration() { }
    public static Integration Create(Guid tenantId, string name)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.");
        var entity = new Integration { TenantId = tenantId, Name = name.Trim() };
        return entity;
    }
}
