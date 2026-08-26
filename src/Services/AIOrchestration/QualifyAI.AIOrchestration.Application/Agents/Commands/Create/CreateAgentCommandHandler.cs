using MediatR;
using QualifyAI.AIOrchestration.Domain.Agents;
namespace QualifyAI.AIOrchestration.Application.Agents.Commands.Create;
public sealed class CreateAgentCommandHandler(IAgentRepository repository) : IRequestHandler<CreateAgentCommand,Guid>
{
    public async Task<Guid> Handle(CreateAgentCommand request, CancellationToken ct)
    {
        var entity = Agent.Create(request.TenantId, request.Name);
        await repository.AddAsync(entity, ct);
        return entity.Id;
    }
}
