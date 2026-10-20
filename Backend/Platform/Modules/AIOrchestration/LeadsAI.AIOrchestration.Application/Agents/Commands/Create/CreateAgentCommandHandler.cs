using MediatR;
using LeadsAI.AIOrchestration.Application.Abstractions.Persistence;
using LeadsAI.AIOrchestration.Domain.Agents;

namespace LeadsAI.AIOrchestration.Application.Agents.Commands.Create;

public sealed class CreateAgentCommandHandler(
    IAgentRepository repository,
    IAIOrchestrationUnitOfWork unitOfWork)
    : IRequestHandler<CreateAgentCommand, Guid>
{
    public async Task<Guid> Handle(CreateAgentCommand request, CancellationToken ct)
    {
        var entity = Agent.Create(request.TenantId, request.Name);
        await repository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return entity.Id;
    }
}
