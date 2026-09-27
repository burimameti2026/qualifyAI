using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Automation.Domain.AutomationDefinitions;
public sealed class AutomationDefinition : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    private AutomationDefinition() { }
    public static AutomationDefinition Create(Guid tenantId, string name)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.");
        var entity = new AutomationDefinition { TenantId = tenantId, Name = name.Trim() };
        return entity;
    }
}
