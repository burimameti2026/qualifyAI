namespace QualifyAI.Application.Abstractions.Persistence;

public sealed record TenantProjection(Guid Id, string Slug, bool IsActive);

public interface ITenantProjectionRepository
{
    Task<TenantProjection?> FindActiveBySlugAsync(string slug, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> ListActiveTenantIdsAsync(CancellationToken cancellationToken = default);
}
