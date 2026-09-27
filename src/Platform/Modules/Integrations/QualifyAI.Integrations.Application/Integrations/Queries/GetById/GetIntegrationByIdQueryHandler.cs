using MediatR;
using QualifyAI.Integrations.Domain.Integrations;
namespace QualifyAI.Integrations.Application.Integrations.Queries.GetById;
public sealed class GetIntegrationByIdQueryHandler(IIntegrationRepository repository) : IRequestHandler<GetIntegrationByIdQuery,IntegrationDto?>
{
    public async Task<IntegrationDto?> Handle(GetIntegrationByIdQuery request, CancellationToken ct)
    {
        var entity = await repository.GetAsync(request.TenantId, request.Id, ct);
        return entity is null ? null : new(entity.Id, entity.TenantId, entity.Name, entity.CreatedAtUtc);
    }
}
