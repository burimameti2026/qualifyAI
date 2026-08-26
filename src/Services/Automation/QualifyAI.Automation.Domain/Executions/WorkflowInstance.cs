using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Automation.Domain.Executions;
public sealed class WorkflowInstance : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public Guid DefinitionId { get; private set; }
    public string Status { get; private set; } = "Running";
    public int CurrentStep { get; private set; }
    public DateTime StartedAtUtc { get; private set; } = DateTime.UtcNow;
    private WorkflowInstance(){}
    public static WorkflowInstance Start(Guid tenantId,Guid definitionId)=>new(){TenantId=tenantId,DefinitionId=definitionId};
    public void Advance()=>CurrentStep++;
    public void Complete()=>Status="Completed";
    public void Fail()=>Status="Failed";
}
