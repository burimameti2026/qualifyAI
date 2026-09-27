using QualifyAI.Identity.Domain.Clients;

namespace QualifyAI.Identity.Application.Abstractions.Persistence;

public interface IClientApplicationRepository
{
    Task<ClientApplication?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ClientApplication?> GetByClientIdAsync(string clientId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ClientApplication>> ListAsync(Guid? tenantId, CancellationToken cancellationToken = default);
    Task<bool> ClientIdExistsAsync(string clientId, CancellationToken cancellationToken = default);
    Task AddAsync(ClientApplication clientApplication, CancellationToken cancellationToken = default);
}
