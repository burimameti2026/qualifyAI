using LeadsAI.BuildingBlocks.Application.CQRS;
namespace LeadsAI.Automation.Application.AutomationDefinitions.Commands.Create;
public sealed record CreateAutomationDefinitionCommand(Guid TenantId, string Name) : ICommand<Guid>;
