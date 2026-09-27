namespace QualifyAI.Domain;

public abstract class Entity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    protected void Touch() => UpdatedAtUtc = DateTime.UtcNow;
}

public abstract class TenantEntity : Entity { public Guid TenantId { get; set; } }
public enum LeadTemperature { Cold, Warm, Hot }
public enum ConversationStatus { Open, Pending, Closed }
public enum TicketStatus { New, Open, Pending, Resolved, Closed }
public enum TicketPriority { Low, Normal, High, Urgent }
public enum OpportunityStatus { Open, Won, Lost }
public enum IntegrationStatus { Disconnected, Connected, Error }
