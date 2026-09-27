namespace QualifyAI.Identity.Application.Authentication;

public interface IUserPermissionReader
{
    Task<IReadOnlyList<string>> ListAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
