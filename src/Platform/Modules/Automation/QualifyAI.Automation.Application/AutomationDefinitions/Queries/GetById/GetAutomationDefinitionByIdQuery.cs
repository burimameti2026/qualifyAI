using QualifyAI.BuildingBlocks.Application.CQRS;
namespace QualifyAI.Automation.Application.AutomationDefinitions.Queries.GetById;
public sealed record GetAutomationDefinitionByIdQuery(Guid TenantId, Guid Id) : IQuery<AutomationDefinitionDto?>;
public sealed record AutomationDefinitionDto(Guid Id, Guid TenantId, string Name, DateTime CreatedAtUtc);
