using LeadsAI.BuildingBlocks.Application.CQRS;
namespace LeadsAI.AIOrchestration.Application.Agents.Commands.Create;
public sealed record CreateAgentCommand(Guid TenantId, string Name) : ICommand<Guid>;
