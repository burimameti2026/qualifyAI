namespace QualifyAI.Identity.Application.Authentication;

public interface ISecurityLifecycleService
{
    Task<IReadOnlyCollection<string>> GenerateMfaRecoveryCodesAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task RevokeSessionsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}
