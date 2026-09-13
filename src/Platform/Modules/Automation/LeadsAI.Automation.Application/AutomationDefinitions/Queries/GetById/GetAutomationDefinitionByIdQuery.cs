using LeadsAI.BuildingBlocks.Application.CQRS;
namespace LeadsAI.Automation.Application.AutomationDefinitions.Queries.GetById;
public sealed record GetAutomationDefinitionByIdQuery(Guid TenantId, Guid Id) : IQuery<AutomationDefinitionDto?>;
public sealed record AutomationDefinitionDto(Guid Id, Guid TenantId, string Name, DateTime CreatedAtUtc);
