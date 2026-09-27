using QualifyAI.BuildingBlocks.Application.CQRS;
namespace QualifyAI.Automation.Application.AutomationDefinitions.Commands.Create;
public sealed record CreateAutomationDefinitionCommand(Guid TenantId, string Name) : ICommand<Guid>;
