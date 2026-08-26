using QualifyAI.BuildingBlocks.Application.CQRS;
namespace QualifyAI.Knowledge.Application.KnowledgeBases.Commands.Create;
public sealed record CreateKnowledgeBaseCommand(Guid TenantId, string Name) : ICommand<Guid>;
