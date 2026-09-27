using QualifyAI.BuildingBlocks.Application.CQRS;
namespace QualifyAI.Integrations.Application.Integrations.Commands.Create;
public sealed record CreateIntegrationCommand(Guid TenantId, string Name) : ICommand<Guid>;
