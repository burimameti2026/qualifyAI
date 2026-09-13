using MediatR;
using LeadsAI.Knowledge.Domain.KnowledgeBases;
namespace LeadsAI.Knowledge.Application.KnowledgeBases.Queries.GetById;
public sealed class GetKnowledgeBaseByIdQueryHandler(IKnowledgeBaseRepository repository) : IRequestHandler<GetKnowledgeBaseByIdQuery,KnowledgeBaseDto?>
{
    public async Task<KnowledgeBaseDto?> Handle(GetKnowledgeBaseByIdQuery request, CancellationToken ct)
    {
        var entity = await repository.GetAsync(request.TenantId, request.Id, ct);
        return entity is null ? null : new(entity.Id, entity.TenantId, entity.Name, entity.CreatedAtUtc);
    }
}
