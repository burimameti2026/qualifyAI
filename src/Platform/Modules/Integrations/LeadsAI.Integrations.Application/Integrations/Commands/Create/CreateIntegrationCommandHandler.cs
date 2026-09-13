using MediatR;
using LeadsAI.Integrations.Application.Abstractions.Persistence;
using LeadsAI.Integrations.Domain.Integrations;

namespace LeadsAI.Integrations.Application.Integrations.Commands.Create;

public sealed class CreateIntegrationCommandHandler(
    IIntegrationRepository repository,
    IIntegrationsUnitOfWork unitOfWork)
    : IRequestHandler<CreateIntegrationCommand, Guid>
{
    public async Task<Guid> Handle(CreateIntegrationCommand request, CancellationToken ct)
    {
        var entity = Integration.Create(request.TenantId, request.Name);
        await repository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return entity.Id;
    }
}
