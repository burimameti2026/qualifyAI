using LeadsAI.Identity.Application.AccessControl;
using LeadsAI.Identity.Application.Authentication;

namespace LeadsAI.Identity.Infrastructure.Authentication;

public sealed class UserPermissionReader(IAccessControlRepository accessControl) : IUserPermissionReader
{
    public Task<IReadOnlyList<string>> ListAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => accessControl.ResolveUserPermissionsAsync(tenantId, userId, cancellationToken);
}
