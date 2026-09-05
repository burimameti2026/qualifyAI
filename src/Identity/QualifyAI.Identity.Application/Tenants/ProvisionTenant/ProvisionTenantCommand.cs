using FluentValidation;
using MediatR;

namespace QualifyAI.Identity.Application.Tenants.ProvisionTenant;

public sealed record ProvisionTenantCommand(
    string Name,
    string Slug,
    string ContactEmail,
    string Plan,
    DateTime StartsAtUtc,
    DateTime? ExpiresAtUtc,
    DateTime? GracePeriodEndsAtUtc,
    int? MaxUsers,
    IReadOnlyCollection<string>? Modules,
    string OwnerEmail,
    string OwnerPassword,
    string OwnerFirstName,
    string OwnerLastName) : IRequest<ProvisionTenantResult>;

public sealed record ProvisionTenantResult(
    Guid TenantId,
    Guid LicenseId,
    Guid OwnerUserId,
    string TenantStatus,
    string LicenseStatus,
    string Plan,
    int MaxUsers,
    IReadOnlyCollection<string> Modules);

public sealed class ProvisionTenantCommandValidator : AbstractValidator<ProvisionTenantCommand>
{
    public ProvisionTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug).NotEmpty().MaximumLength(100).Matches("^[a-zA-Z0-9-]+$");
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.Plan).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerEmail).NotEmpty().EmailAddress().MaximumLength(320);
        RuleFor(x => x.OwnerPassword).NotEmpty().MinimumLength(10);
        RuleFor(x => x.OwnerFirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.OwnerLastName).NotEmpty().MaximumLength(100);
    }
}

public interface ITenantProvisioningService
{
    Task<ProvisionTenantResult> ProvisionAsync(ProvisionTenantCommand request, CancellationToken cancellationToken);
}

public sealed class ProvisionTenantCommandHandler(ITenantProvisioningService provisioning)
    : IRequestHandler<ProvisionTenantCommand, ProvisionTenantResult>
{
    public Task<ProvisionTenantResult> Handle(ProvisionTenantCommand request, CancellationToken cancellationToken)
        => provisioning.ProvisionAsync(request, cancellationToken);
}
