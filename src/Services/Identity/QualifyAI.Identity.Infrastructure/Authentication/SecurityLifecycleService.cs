using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Infrastructure.Identity;

namespace QualifyAI.Identity.Infrastructure.Authentication;

public sealed class SecurityLifecycleService(UserManager<ApplicationUser> users) : ISecurityLifecycleService
{
    public async Task<IReadOnlyCollection<string>> GenerateMfaRecoveryCodesAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(tenantId, userId, ct);
        if (!user.TwoFactorEnabled)
            throw new InvalidOperationException("MFA must be enabled before recovery codes can be generated.");

        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10);
        await users.UpdateSecurityStampAsync(user);
        return codes?.ToArray() ?? [];
    }

    public async Task RevokeSessionsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(tenantId, userId, ct);
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(x => x.Description)));
    }

    private async Task<ApplicationUser> GetUserAsync(Guid tenantId, Guid userId, CancellationToken ct)
        => await users.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId, ct)
           ?? throw new KeyNotFoundException("User not found.");
}
