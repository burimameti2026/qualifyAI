using System.Security.Cryptography;
using FluentValidation;
using MediatR;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Domain.Clients;
using QualifyAI.Identity.Application;

namespace QualifyAI.Identity.Application.Clients.RegisterClient;

public sealed record RegisterClientApplicationCommand(
    Guid? TenantId,
    string ClientId,
    string DisplayName,
    IReadOnlyCollection<string> Scopes) : IRequest<RegisterClientApplicationResult>;

public sealed record RegisterClientApplicationResult(
    Guid Id,
    Guid? TenantId,
    string ClientId,
    string DisplayName,
    string ClientSecret,
    IReadOnlyCollection<string> Scopes);

public sealed class RegisterClientApplicationCommandValidator
    : AbstractValidator<RegisterClientApplicationCommand>
{
    public RegisterClientApplicationCommandValidator()
    {
        RuleFor(x => x.ClientId)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[a-zA-Z0-9._-]+$");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Scopes).NotNull().Must(x => x.Count > 0);
    }
}

public sealed class RegisterClientApplicationCommandHandler(
    IClientApplicationRepository clients,
    IClientCredentialStore credentialStore,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<RegisterClientApplicationCommand, RegisterClientApplicationResult>
{
    public async Task<RegisterClientApplicationResult> Handle(
        RegisterClientApplicationCommand request,
        CancellationToken cancellationToken)
    {
        var clientId = request.ClientId.Trim().ToLowerInvariant();
        if (await clients.ClientIdExistsAsync(clientId, cancellationToken))
            throw new IdentityConflictException($"Client '{clientId}' already exists.");

        var client = ClientApplication.Create(
            request.TenantId,
            clientId,
            request.DisplayName,
            request.Scopes);

        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

        await credentialStore.RegisterAsync(
            client.ClientId,
            client.DisplayName,
            secret,
            client.Scopes.Select(x => x.Name).ToArray(),
            cancellationToken);

        await clients.AddAsync(client, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new RegisterClientApplicationResult(
            client.Id,
            client.TenantId,
            client.ClientId,
            client.DisplayName,
            secret,
            client.Scopes.Select(x => x.Name).ToArray());
    }
}
