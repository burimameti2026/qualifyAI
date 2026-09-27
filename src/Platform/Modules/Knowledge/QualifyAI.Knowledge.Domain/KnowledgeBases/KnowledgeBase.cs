using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Knowledge.Domain.KnowledgeBases;
public sealed class KnowledgeBase : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    private KnowledgeBase() { }
    public static KnowledgeBase Create(Guid tenantId, string name)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.");
        var entity = new KnowledgeBase { TenantId = tenantId, Name = name.Trim() };
        return entity;
    }
}
