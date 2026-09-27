using QualifyAI.BuildingBlocks.Domain.Abstractions;
namespace QualifyAI.AIOrchestration.Domain.Agents;
public sealed class AiAgent : AggregateRoot
{
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public string Role { get; private set; } = "";
    public AgentStatus Status { get; private set; } = AgentStatus.Draft;
    public int Version { get; private set; } = 1;
    private readonly List<AgentToolPermission> _tools = [];
    public IReadOnlyCollection<AgentToolPermission> Tools => _tools;
    private AiAgent() {}
    public static AiAgent Create(Guid tenantId,string name,string role) => new(){TenantId=tenantId,Name=name,Role=role};
    public void AllowTool(string toolName) { if (_tools.All(x=>x.ToolName!=toolName)) _tools.Add(new(toolName,true)); }
    public void Publish(){Status=AgentStatus.Published;Version++;}
}
