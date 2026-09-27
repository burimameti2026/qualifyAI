using MediatR;
using QualifyAI.Automation.Application.Abstractions.Persistence;
using QualifyAI.Automation.Domain.AutomationDefinitions;

namespace QualifyAI.Automation.Application.AutomationDefinitions.Commands.Create;

public sealed class CreateAutomationDefinitionCommandHandler(
    IAutomationDefinitionRepository repository,
    IAutomationUnitOfWork unitOfWork)
    : IRequestHandler<CreateAutomationDefinitionCommand, Guid>
{
    public async Task<Guid> Handle(CreateAutomationDefinitionCommand request, CancellationToken ct)
    {
        var entity = AutomationDefinition.Create(request.TenantId, request.Name);
        await repository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return entity.Id;
    }
}
