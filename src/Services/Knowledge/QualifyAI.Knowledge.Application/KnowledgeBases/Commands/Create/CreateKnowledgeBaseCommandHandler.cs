using MediatR;
using QualifyAI.Knowledge.Application.Abstractions.Persistence;
using QualifyAI.Knowledge.Domain.KnowledgeBases;

namespace QualifyAI.Knowledge.Application.KnowledgeBases.Commands.Create;

public sealed class CreateKnowledgeBaseCommandHandler(
    IKnowledgeBaseRepository repository,
    IKnowledgeUnitOfWork unitOfWork)
    : IRequestHandler<CreateKnowledgeBaseCommand, Guid>
{
    public async Task<Guid> Handle(CreateKnowledgeBaseCommand request, CancellationToken ct)
    {
        var entity = KnowledgeBase.Create(request.TenantId, request.Name);
        await repository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return entity.Id;
    }
}
