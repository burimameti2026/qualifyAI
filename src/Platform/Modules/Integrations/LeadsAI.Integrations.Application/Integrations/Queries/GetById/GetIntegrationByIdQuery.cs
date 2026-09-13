using LeadsAI.BuildingBlocks.Application.CQRS;
namespace LeadsAI.Integrations.Application.Integrations.Queries.GetById;
public sealed record GetIntegrationByIdQuery(Guid TenantId, Guid Id) : IQuery<IntegrationDto?>;
public sealed record IntegrationDto(Guid Id, Guid TenantId, string Name, DateTime CreatedAtUtc);
