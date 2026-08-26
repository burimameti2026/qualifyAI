using Microsoft.EntityFrameworkCore;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Infrastructure.Persistence;

namespace QualifyAI.Identity.Infrastructure.Authentication;

public sealed class UserPermissionReader(IdentityDbContext dbContext) : IUserPermissionReader
{
    public async Task<IReadOnlyList<string>> ListAsync(
        Guid tenantId,
        Guid userId,
        CancellationToken cancellationToken = default)
        => await dbContext.UserPermissions.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.UserId == userId)
            .Select(x => x.Permission)
            .ToListAsync(cancellationToken);
}
