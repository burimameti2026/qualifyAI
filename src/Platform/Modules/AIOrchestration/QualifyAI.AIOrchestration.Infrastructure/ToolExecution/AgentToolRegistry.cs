using QualifyAI.AIOrchestration.Application.Tools;
namespace QualifyAI.AIOrchestration.Infrastructure.ToolExecution;
public sealed class AgentToolRegistry(IEnumerable<IAgentTool> tools) : IAgentToolRegistry
{
    private readonly Dictionary<string,IAgentTool> _tools = tools.ToDictionary(x=>x.Name,StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<IAgentTool> All => _tools.Values.ToArray();
    public IAgentTool GetRequired(string name) => _tools.TryGetValue(name,out var tool) ? tool : throw new KeyNotFoundException($"AI tool '{name}' is not registered.");
}
