using MediatR;
using QualifyAI.Integrations.Application.Abstractions.Persistence;
using QualifyAI.Integrations.Domain.Integrations;

namespace QualifyAI.Integrations.Application.Integrations.Commands.Create;

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
