using MediatR;
using LeadsAI.BuildingBlocks.Messaging.Outbox;
using LeadsAI.Contracts.Identity;
using LeadsAI.Identity.Application.Abstractions.Persistence;
using LeadsAI.Identity.Domain.Tenants;
using LeadsAI.Identity.Application;

namespace LeadsAI.Identity.Application.Tenants.SetStatus;

public sealed record SetTenantStatusCommand(Guid TenantId, TenantStatus Status) : IRequest;

public sealed class SetTenantStatusCommandHandler(
    ITenantRepository tenants,
    IOutboxWriter outbox,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<SetTenantStatusCommand>
{
    public async Task Handle(
        SetTenantStatusCommand request,
        CancellationToken cancellationToken)
    {
        var tenant = await tenants.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Tenant not found.");

        switch (request.Status)
        {
            case TenantStatus.Active:
                tenant.Activate();
                break;
            case TenantStatus.Suspended:
                tenant.Suspend();
                break;
            default:
                throw new IdentityConflictException($"Tenant status transition to '{request.Status}' is not supported by this command.");
        }

        outbox.Add(new TenantStatusChangedIntegrationEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            tenant.Id,
            tenant.Slug,
            tenant.Status.ToString()));

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
