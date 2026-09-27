namespace QualifyAI.Domain;

public sealed class AutonomousAcquisitionAgentMemory
{
 public Guid Id { get; set; }=Guid.NewGuid();
 public Guid TenantId { get; set; }
 public Guid AgentId { get; set; }
 public string Key { get; set; }=string.Empty;
 public string Value { get; set; }=string.Empty;
 public string Category { get; set; }="general";
 public DateTime CreatedAtUtc { get; set; }=DateTime.UtcNow;
 public DateTime UpdatedAtUtc { get; set; }=DateTime.UtcNow;
}
