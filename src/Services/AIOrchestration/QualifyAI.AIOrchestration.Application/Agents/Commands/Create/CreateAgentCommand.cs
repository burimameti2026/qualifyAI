using QualifyAI.BuildingBlocks.Application.CQRS;
namespace QualifyAI.AIOrchestration.Application.Agents.Commands.Create;
public sealed record CreateAgentCommand(Guid TenantId, string Name) : ICommand<Guid>;
