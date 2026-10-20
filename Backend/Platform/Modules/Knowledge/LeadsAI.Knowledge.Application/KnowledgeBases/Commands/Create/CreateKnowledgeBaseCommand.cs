using LeadsAI.BuildingBlocks.Application.CQRS;
namespace LeadsAI.Knowledge.Application.KnowledgeBases.Commands.Create;
public sealed record CreateKnowledgeBaseCommand(Guid TenantId, string Name) : ICommand<Guid>;
