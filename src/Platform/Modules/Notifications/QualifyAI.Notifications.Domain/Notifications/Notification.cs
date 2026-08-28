using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Notifications.Domain.Notifications;
public sealed class Notification : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    private Notification() { }
    public static Notification Create(Guid tenantId, string name)
    {
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant is required.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name is required.");
        var entity = new Notification { TenantId = tenantId, Name = name.Trim() };
        return entity;
    }
}
