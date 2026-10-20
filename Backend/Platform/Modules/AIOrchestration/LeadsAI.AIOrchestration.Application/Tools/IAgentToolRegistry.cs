namespace LeadsAI.AIOrchestration.Application.Tools;
public interface IAgentToolRegistry
{
    IReadOnlyCollection<IAgentTool> All { get; }
    IAgentTool GetRequired(string name);
}
