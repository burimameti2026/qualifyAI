using MediatR;
using LeadsAI.Automation.Domain.AutomationDefinitions;
namespace LeadsAI.Automation.Application.AutomationDefinitions.Queries.GetById;
public sealed class GetAutomationDefinitionByIdQueryHandler(IAutomationDefinitionRepository repository) : IRequestHandler<GetAutomationDefinitionByIdQuery,AutomationDefinitionDto?>
{
    public async Task<AutomationDefinitionDto?> Handle(GetAutomationDefinitionByIdQuery request, CancellationToken ct)
    {
        var entity = await repository.GetAsync(request.TenantId, request.Id, ct);
        return entity is null ? null : new(entity.Id, entity.TenantId, entity.Name, entity.CreatedAtUtc);
    }
}
