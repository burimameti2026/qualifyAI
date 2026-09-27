using QualifyAI.BuildingBlocks.Application.CQRS;
namespace QualifyAI.AIOrchestration.Application.Agents.Queries.GetById;
public sealed record GetAgentByIdQuery(Guid TenantId, Guid Id) : IQuery<AgentDto?>;
public sealed record AgentDto(Guid Id, Guid TenantId, string Name, DateTime CreatedAtUtc);
