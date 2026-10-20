using LeadsAI.BuildingBlocks.Application.CQRS;
namespace LeadsAI.Knowledge.Application.KnowledgeBases.Queries.GetById;
public sealed record GetKnowledgeBaseByIdQuery(Guid TenantId, Guid Id) : IQuery<KnowledgeBaseDto?>;
public sealed record KnowledgeBaseDto(Guid Id, Guid TenantId, string Name, DateTime CreatedAtUtc);
