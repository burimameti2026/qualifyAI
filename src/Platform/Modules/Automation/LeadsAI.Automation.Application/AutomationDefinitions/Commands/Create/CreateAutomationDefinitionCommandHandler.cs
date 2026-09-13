using MediatR;
using LeadsAI.Automation.Application.Abstractions.Persistence;
using LeadsAI.Automation.Domain.AutomationDefinitions;

namespace LeadsAI.Automation.Application.AutomationDefinitions.Commands.Create;

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
