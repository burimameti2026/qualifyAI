using MediatR;
using QualifyAI.Automation.Domain.AutomationDefinitions;
namespace QualifyAI.Automation.Application.AutomationDefinitions.Queries.GetById;
public sealed class GetAutomationDefinitionByIdQueryHandler(IAutomationDefinitionRepository repository) : IRequestHandler<GetAutomationDefinitionByIdQuery,AutomationDefinitionDto?>
{
    public async Task<AutomationDefinitionDto?> Handle(GetAutomationDefinitionByIdQuery request, CancellationToken ct)
    {
        var entity = await repository.GetAsync(request.TenantId, request.Id, ct);
        return entity is null ? null : new(entity.Id, entity.TenantId, entity.Name, entity.CreatedAtUtc);
    }
}
