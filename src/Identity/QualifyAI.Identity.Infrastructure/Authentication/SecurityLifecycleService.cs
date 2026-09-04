using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Application;
using QualifyAI.Identity.Persistence.SqlServer.Identity;

namespace QualifyAI.Identity.Infrastructure.Authentication;

public sealed class SecurityLifecycleService(UserManager<ApplicationUser> users) : ISecurityLifecycleService
{
    public async Task<IReadOnlyCollection<string>> GenerateMfaRecoveryCodesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(tenantId, userId, ct);
        if (!user.TwoFactorEnabled)
            throw new IdentityConflictException("MFA must be enabled before recovery codes can be generated.");

        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await users.UpdateSecurityStampAsync(user);
        return codes?.ToArray() ?? [];
    }

    public async Task RevokeSessionsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(tenantId, userId, ct);
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
            throw new IdentityValidationException(result.Errors
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Code) ? "identity" : x.Code)
                .ToDictionary(x => x.Key, x => x.Select(y => y.Description).Distinct().ToArray()));
    }

    private async Task<ApplicationUser> GetUserAsync(Guid tenantId, Guid userId, CancellationToken ct)
        => await users.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId, ct)
           ?? throw new KeyNotFoundException("User not found.");
}
