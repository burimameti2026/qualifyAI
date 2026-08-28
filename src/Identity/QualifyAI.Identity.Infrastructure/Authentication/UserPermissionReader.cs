using QualifyAI.Identity.Application.AccessControl;
using QualifyAI.Identity.Application.Authentication;

namespace QualifyAI.Identity.Infrastructure.Authentication;

public sealed class UserPermissionReader(IAccessControlRepository accessControl) : IUserPermissionReader
{
    public Task<IReadOnlyList<string>> ListAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => accessControl.ResolveUserPermissionsAsync(tenantId, userId, cancellationToken);
}
