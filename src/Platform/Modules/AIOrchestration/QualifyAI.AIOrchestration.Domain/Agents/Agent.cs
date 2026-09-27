using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.AIOrchestration.Domain.Agents;
public sealed class Agent : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    private Agent() { }
    public static Agent Create(Guid tenantId, string name)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.");
        var entity = new Agent { TenantId = tenantId, Name = name.Trim() };
        return entity;
    }
}
