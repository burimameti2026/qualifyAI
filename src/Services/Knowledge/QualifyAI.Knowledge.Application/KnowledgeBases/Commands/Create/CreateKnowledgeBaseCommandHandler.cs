using MediatR;
using QualifyAI.Knowledge.Domain.KnowledgeBases;
namespace QualifyAI.Knowledge.Application.KnowledgeBases.Commands.Create;
public sealed class CreateKnowledgeBaseCommandHandler(IKnowledgeBaseRepository repository) : IRequestHandler<CreateKnowledgeBaseCommand,Guid>
{
    public async Task<Guid> Handle(CreateKnowledgeBaseCommand request, CancellationToken ct)
    {
        var entity = KnowledgeBase.Create(request.TenantId, request.Name);
        await repository.AddAsync(entity, ct);
        return entity.Id;
    }
}
