using FluentValidation;
using MediatR;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Contracts.Identity;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Domain.Licensing;

namespace QualifyAI.Identity.Application.Licensing.UpdateLicense;

public sealed record UpdateLicenseCommand(
    Guid TenantId,
    string Plan,
    int MaxUsers,
    DateTime? ExpiresAtUtc,
    IReadOnlyCollection<string> Modules) : IRequest;

public sealed class UpdateLicenseCommandValidator : AbstractValidator<UpdateLicenseCommand>
{
    public UpdateLicenseCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Plan).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MaxUsers).GreaterThan(0);
        RuleFor(x => x.Modules).NotNull();
    }
}

public sealed class UpdateLicenseCommandHandler(
    ILicenseRepository licenses,
    IOutboxWriter outbox,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<UpdateLicenseCommand>
{
    public async Task Handle(
        UpdateLicenseCommand request,
        CancellationToken cancellationToken)
    {
        var license = await licenses.GetByTenantIdAsync(request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("License not found.");

        license.ChangePlan(request.Plan, request.MaxUsers, request.ExpiresAtUtc);
        license.ReplaceModules(request.Modules);

        QueueLicenseChanged(outbox, license);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    internal static void QueueLicenseChanged(IOutboxWriter outbox, License license)
    {
        outbox.Add(new TenantLicenseChangedIntegrationEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            license.TenantId,
            license.Id,
            license.Plan,
            license.Status.ToString(),
            license.MaxUsers,
            license.StartsAtUtc,
            license.ExpiresAtUtc,
            license.Version,
            license.Modules.Where(x => x.Enabled).Select(x => x.Code).ToArray()));
    }
}
