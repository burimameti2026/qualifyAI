namespace QualifyAI.Identity.Application.Clients;

public interface IClientCredentialStore
{
    Task RegisterAsync(
        string clientId,
        string displayName,
        string clientSecret,
        IReadOnlyCollection<string> scopes,
        CancellationToken cancellationToken = default);

    Task RotateSecretAsync(
        string clientId,
        string newClientSecret,
        CancellationToken cancellationToken = default);

    Task SetEnabledAsync(
        string clientId,
        bool enabled,
        CancellationToken cancellationToken = default);
}
