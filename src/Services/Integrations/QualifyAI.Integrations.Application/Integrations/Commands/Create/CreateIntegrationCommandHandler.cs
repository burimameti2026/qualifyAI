using MediatR;
using QualifyAI.Integrations.Domain.Integrations;
namespace QualifyAI.Integrations.Application.Integrations.Commands.Create;
public sealed class CreateIntegrationCommandHandler(IIntegrationRepository repository) : IRequestHandler<CreateIntegrationCommand,Guid>
{
    public async Task<Guid> Handle(CreateIntegrationCommand request, CancellationToken ct)
    {
        var entity = Integration.Create(request.TenantId, request.Name);
        await repository.AddAsync(entity, ct);
        return entity.Id;
    }
}
