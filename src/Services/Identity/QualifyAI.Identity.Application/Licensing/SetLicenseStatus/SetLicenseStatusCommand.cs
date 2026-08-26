using MediatR;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Application.Licensing.UpdateLicense;
using QualifyAI.Identity.Domain.Licensing;

namespace QualifyAI.Identity.Application.Licensing.SetLicenseStatus;

public sealed record SetLicenseStatusCommand(Guid TenantId, LicenseStatus Status) : IRequest;

public sealed class SetLicenseStatusCommandHandler(
    ILicenseRepository licenses,
    IOutboxWriter outbox,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<SetLicenseStatusCommand>
{
    public async Task Handle(
        SetLicenseStatusCommand request,
        CancellationToken cancellationToken)
    {
        var license = await licenses.GetByTenantIdAsync(request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("License not found.");

        switch (request.Status)
        {
            case LicenseStatus.Active:
                license.Activate();
                break;
            case LicenseStatus.Suspended:
                license.Suspend();
                break;
            case LicenseStatus.Cancelled:
                license.Cancel();
                break;
            default:
                throw new InvalidOperationException($"License status transition to '{request.Status}' is not supported by this command.");
        }

        UpdateLicenseCommandHandler.QueueLicenseChanged(outbox, license);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
