using MediatR;
using QualifyAI.AIOrchestration.Domain.Agents;
namespace QualifyAI.AIOrchestration.Application.Agents.Queries.GetById;
public sealed class GetAgentByIdQueryHandler(IAgentRepository repository) : IRequestHandler<GetAgentByIdQuery,AgentDto?>
{
    public async Task<AgentDto?> Handle(GetAgentByIdQuery request, CancellationToken ct)
    {
        var entity = await repository.GetAsync(request.TenantId, request.Id, ct);
        return entity is null ? null : new(entity.Id, entity.TenantId, entity.Name, entity.CreatedAtUtc);
    }
}
