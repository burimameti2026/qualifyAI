using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.Automation.Domain.Definitions;
public sealed class AutomationDefinition : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public int Version { get; private set; } = 1;
    public bool IsPublished { get; private set; }
    private readonly List<AutomationStep> _steps = [];
    public IReadOnlyCollection<AutomationStep> Steps => _steps;
    private AutomationDefinition() {}
    public static AutomationDefinition Create(Guid tenantId,string name)=>new(){TenantId=tenantId,Name=name};
    public void AddStep(AutomationStep step)=>_steps.Add(step);
    public void Publish(){IsPublished=true;Version++;}
}
