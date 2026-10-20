using LeadsAI.BuildingBlocks.Application.CQRS;
namespace LeadsAI.Integrations.Application.Integrations.Commands.Create;
public sealed record CreateIntegrationCommand(Guid TenantId, string Name) : ICommand<Guid>;
