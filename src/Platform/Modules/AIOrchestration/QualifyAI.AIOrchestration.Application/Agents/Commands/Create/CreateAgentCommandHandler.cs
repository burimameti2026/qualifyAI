using MediatR;
using QualifyAI.AIOrchestration.Application.Abstractions.Persistence;
using QualifyAI.AIOrchestration.Domain.Agents;

namespace QualifyAI.AIOrchestration.Application.Agents.Commands.Create;

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
