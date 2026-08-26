using FluentValidation;
using MediatR;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Contracts.Identity;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Domain.Tenants;

namespace QualifyAI.Identity.Application.Tenants.CreateTenant;

public sealed record CreateTenantCommand(
    string Name,
    string Slug,
    string ContactEmail) : IRequest<CreateTenantResult>;

public sealed record CreateTenantResult(
    Guid Id,
    string Name,
    string Slug,
    string ContactEmail,
    string Status);

public sealed class CreateTenantCommandValidator : AbstractValidator<CreateTenantCommand>
{
    public CreateTenantCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Slug)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-zA-Z0-9-]+$");
        RuleFor(x => x.ContactEmail).NotEmpty().EmailAddress().MaximumLength(320);
    }
}

public sealed class CreateTenantCommandHandler(
    ITenantRepository tenants,
    IOutboxWriter outbox,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<CreateTenantCommand, CreateTenantResult>
{
    public async Task<CreateTenantResult> Handle(
        CreateTenantCommand request,
        CancellationToken cancellationToken)
    {
        var slug = request.Slug.Trim().ToLowerInvariant();

        if (await tenants.SlugExistsAsync(slug, cancellationToken))
            throw new InvalidOperationException($"Tenant slug '{slug}' already exists.");

        var tenant = Tenant.Create(request.Name, slug, request.ContactEmail);
        await tenants.AddAsync(tenant, cancellationToken);

        outbox.Add(new TenantCreatedIntegrationEvent(
            Guid.NewGuid(),
            DateTime.UtcNow,
            tenant.Id,
            tenant.Slug,
            tenant.Name,
            tenant.ContactEmail));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CreateTenantResult(
            tenant.Id,
            tenant.Name,
            tenant.Slug,
            tenant.ContactEmail,
            tenant.Status.ToString());
    }
}
