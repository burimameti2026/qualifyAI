using FluentValidation;
using MediatR;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Contracts.Identity;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Application.Licensing;
using QualifyAI.Identity.Domain.Licensing;

namespace QualifyAI.Identity.Application.Licensing.AssignLicense;

public sealed record AssignLicenseCommand(
    Guid TenantId,
    string Plan,
    DateTime StartsAtUtc,
    DateTime? ExpiresAtUtc,
    int MaxUsers,
    IReadOnlyCollection<string> Modules) : IRequest<LicenseResult>;

public sealed record LicenseResult(
    Guid Id,
    Guid TenantId,
    string Plan,
    string Status,
    int MaxUsers,
    DateTime StartsAtUtc,
    DateTime? ExpiresAtUtc,
    long Version,
    IReadOnlyCollection<string> Modules);

public sealed class AssignLicenseCommandValidator : AbstractValidator<AssignLicenseCommand>
{
    public AssignLicenseCommandValidator()
    {
        RuleFor(x => x.TenantId).NotEmpty();
        RuleFor(x => x.Plan).NotEmpty().MaximumLength(100);
        RuleFor(x => x.MaxUsers).GreaterThan(0);
        RuleFor(x => x.Modules).NotNull();
    }
}

public sealed class AssignLicenseCommandHandler(
    ITenantRepository tenants,
    ILicenseRepository licenses,
    IOutboxWriter outbox,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<AssignLicenseCommand, LicenseResult>
{
    public async Task<LicenseResult> Handle(
        AssignLicenseCommand request,
        CancellationToken cancellationToken)
    {
        _ = await tenants.GetByIdAsync(request.TenantId, cancellationToken)
            ?? throw new KeyNotFoundException("Tenant not found.");

        var existing = await licenses.GetByTenantIdAsync(request.TenantId, cancellationToken);
        if (existing is not null)
            throw new InvalidOperationException("Tenant already has a license. Use the license update flow.");

        var modules = LicensePlanCatalog.ValidateModules(request.Plan, request.Modules);
        var license = License.Create(
            request.TenantId,
            request.Plan,
            request.StartsAtUtc,
            request.ExpiresAtUtc,
            request.MaxUsers,
            modules);

        await licenses.AddAsync(license, cancellationToken);

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

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return ToResult(license);
    }

    internal static LicenseResult ToResult(License license)
        => new(
            license.Id,
            license.TenantId,
            license.Plan,
            license.Status.ToString(),
            license.MaxUsers,
            license.StartsAtUtc,
            license.ExpiresAtUtc,
            license.Version,
            license.Modules.Where(x => x.Enabled).Select(x => x.Code).ToArray());
}
