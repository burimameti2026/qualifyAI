using System.Security.Cryptography;
using MediatR;
using QualifyAI.Identity.Application.Abstractions.Persistence;
using QualifyAI.Identity.Domain.Clients;

namespace QualifyAI.Identity.Application.Clients.ManageClients;

public sealed record ListClientApplicationsQuery(Guid? TenantId) : IRequest<IReadOnlyList<ClientApplicationResult>>;
public sealed record RotateClientSecretCommand(Guid ClientApplicationId) : IRequest<RotateClientSecretResult>;
public sealed record SetClientApplicationStatusCommand(Guid ClientApplicationId, bool Enabled) : IRequest;

public sealed record ClientApplicationResult(
    Guid Id,
    Guid? TenantId,
    string ClientId,
    string DisplayName,
    string Status,
    IReadOnlyCollection<string> Scopes,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record RotateClientSecretResult(string ClientId, string ClientSecret);

public sealed class ListClientApplicationsQueryHandler(IClientApplicationRepository clients)
    : IRequestHandler<ListClientApplicationsQuery, IReadOnlyList<ClientApplicationResult>>
{
    public async Task<IReadOnlyList<ClientApplicationResult>> Handle(
        ListClientApplicationsQuery request,
        CancellationToken cancellationToken)
    {
        var items = await clients.ListAsync(request.TenantId, cancellationToken);
        return items.Select(Map).ToArray();
    }

    internal static ClientApplicationResult Map(ClientApplication client)
        => new(
            client.Id,
            client.TenantId,
            client.ClientId,
            client.DisplayName,
            client.Status.ToString(),
            client.Scopes.Select(x => x.Name).ToArray(),
            client.CreatedAtUtc,
            client.UpdatedAtUtc);
}

public sealed class RotateClientSecretCommandHandler(
    IClientApplicationRepository clients,
    IClientCredentialStore credentialStore)
    : IRequestHandler<RotateClientSecretCommand, RotateClientSecretResult>
{
    public async Task<RotateClientSecretResult> Handle(
        RotateClientSecretCommand request,
        CancellationToken cancellationToken)
    {
        var client = await clients.GetByIdAsync(request.ClientApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Client application not found.");

        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        await credentialStore.RotateSecretAsync(client.ClientId, secret, cancellationToken);
        return new RotateClientSecretResult(client.ClientId, secret);
    }
}

public sealed class SetClientApplicationStatusCommandHandler(
    IClientApplicationRepository clients,
    IClientCredentialStore credentialStore,
    IIdentityUnitOfWork unitOfWork)
    : IRequestHandler<SetClientApplicationStatusCommand>
{
    public async Task Handle(
        SetClientApplicationStatusCommand request,
        CancellationToken cancellationToken)
    {
        var client = await clients.GetByIdAsync(request.ClientApplicationId, cancellationToken)
            ?? throw new KeyNotFoundException("Client application not found.");

        if (request.Enabled)
            client.Enable();
        else
            client.Disable();

        await credentialStore.SetEnabledAsync(client.ClientId, request.Enabled, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
