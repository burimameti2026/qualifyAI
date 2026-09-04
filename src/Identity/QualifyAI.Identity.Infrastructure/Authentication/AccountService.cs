using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Messaging.Outbox;
using QualifyAI.Contracts.Identity;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Application;
using QualifyAI.Identity.Persistence.SqlServer.Identity;
using QualifyAI.Identity.Persistence.SqlServer;

namespace QualifyAI.Identity.Infrastructure.Authentication;

public sealed class AccountService(
    UserManager<ApplicationUser> users,
    RoleManager<ApplicationRole> roles,
    IdentityDbContext dbContext,
    IOutboxWriter outbox) : IAccountService
{
    public async Task<AccountResult> CreateUserAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();
        var existing = await users.Users.FirstOrDefaultAsync(x => x.TenantId == request.TenantId && x.NormalizedEmail == normalizedEmail, cancellationToken);
        if (existing is not null) throw new IdentityConflictException("User already exists in this tenant.");

        return await ExecuteInTransactionAsync(async () =>
        {
            var user = new ApplicationUser { Id = Guid.NewGuid(), TenantId = request.TenantId, TenantSlug = request.TenantSlug.Trim().ToLowerInvariant(), UserName = request.Email.Trim().ToLowerInvariant(), Email = request.Email.Trim().ToLowerInvariant(), FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), EmailConfirmed = true, IsActive = true };
            EnsureSucceeded(await users.CreateAsync(user, request.Password));
            await SetRolesCoreAsync(user, request.Roles, cancellationToken);
            await users.UpdateSecurityStampAsync(user);
            await QueueAccessChangedAsync(user, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return await MapAsync(user, cancellationToken);
        }, cancellationToken);
    }

    public async Task<AccountResult?> GetUserAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    { var user = await users.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId, cancellationToken); return user is null ? null : await MapAsync(user, cancellationToken); }

    public async Task<IReadOnlyList<AccountResult>> ListUsersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    { var tenantUsers = await users.Users.Where(x => x.TenantId == tenantId).OrderBy(x => x.Email).ToListAsync(cancellationToken); var result = new List<AccountResult>(tenantUsers.Count); foreach (var user in tenantUsers) result.Add(await MapAsync(user, cancellationToken)); return result; }

    public Task SetRolesAsync(Guid tenantId, Guid userId, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken = default)
        => ExecuteInTransactionAsync(async () => { var user = await GetEntityAsync(tenantId, userId, cancellationToken); await SetRolesCoreAsync(user, roleNames, cancellationToken); await users.UpdateSecurityStampAsync(user); await QueueAccessChangedAsync(user, cancellationToken); await dbContext.SaveChangesAsync(cancellationToken); return 0; }, cancellationToken);

    public Task SetPermissionsAsync(Guid tenantId, Guid userId, IReadOnlyCollection<string> permissions, CancellationToken cancellationToken = default)
        => ExecuteInTransactionAsync(async () =>
        {
            var user = await GetEntityAsync(tenantId, userId, cancellationToken);
            var existing = await dbContext.UserPermissions.Where(x => x.TenantId == tenantId && x.UserId == userId).ToListAsync(cancellationToken);
            dbContext.UserPermissions.RemoveRange(existing);
            dbContext.UserPermissions.AddRange(permissions.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Select(permission => new UserPermission { TenantId = tenantId, UserId = userId, Permission = permission }));
            await dbContext.SaveChangesAsync(cancellationToken);
            await users.UpdateSecurityStampAsync(user);
            await QueueAccessChangedAsync(user, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }, cancellationToken);

    public Task DisableAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) => SetActiveAsync(tenantId, userId, false, cancellationToken);
    public Task EnableAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) => SetActiveAsync(tenantId, userId, true, cancellationToken);
    public async Task ChangePasswordAsync(Guid tenantId, Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) { var user = await GetEntityAsync(tenantId, userId, cancellationToken); EnsureSucceeded(await users.ChangePasswordAsync(user, currentPassword, newPassword)); }
    public async Task<string> GeneratePasswordResetTokenAsync(Guid tenantId, string email, CancellationToken cancellationToken = default) { var user = await FindByTenantEmailAsync(tenantId, email, cancellationToken) ?? throw new KeyNotFoundException("User not found."); return await users.GeneratePasswordResetTokenAsync(user); }
    public async Task ResetPasswordAsync(Guid tenantId, string email, string token, string newPassword, CancellationToken cancellationToken = default) { var user = await FindByTenantEmailAsync(tenantId, email, cancellationToken) ?? throw new KeyNotFoundException("User not found."); EnsureSucceeded(await users.ResetPasswordAsync(user, token, newPassword)); }

    public async Task<MfaSetupResult> BeginMfaAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
    { var user = await GetEntityAsync(tenantId, userId, cancellationToken); var key = await users.GetAuthenticatorKeyAsync(user); if (string.IsNullOrWhiteSpace(key)) { await users.ResetAuthenticatorKeyAsync(user); key = await users.GetAuthenticatorKeyAsync(user); } var accountName = Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id.ToString()); return new MfaSetupResult(key ?? string.Empty, $"otpauth://totp/QualifyAI:{accountName}?secret={key}&issuer=QualifyAI&digits=6"); }
    public async Task<bool> ConfirmMfaAsync(Guid tenantId, Guid userId, string code, CancellationToken cancellationToken = default) { var user = await GetEntityAsync(tenantId, userId, cancellationToken); var valid = await users.VerifyTwoFactorTokenAsync(user, TokenOptions.DefaultAuthenticatorProvider, code.Replace(" ", string.Empty).Replace("-", string.Empty)); if (valid) EnsureSucceeded(await users.SetTwoFactorEnabledAsync(user, true)); return valid; }
    public async Task DisableMfaAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default) { var user = await GetEntityAsync(tenantId, userId, cancellationToken); EnsureSucceeded(await users.SetTwoFactorEnabledAsync(user, false)); await users.ResetAuthenticatorKeyAsync(user); }

    private Task SetActiveAsync(Guid tenantId, Guid userId, bool isActive, CancellationToken cancellationToken)
        => ExecuteInTransactionAsync(async () => { var user = await GetEntityAsync(tenantId, userId, cancellationToken); if (user.IsActive == isActive) return 0; user.IsActive = isActive; EnsureSucceeded(await users.UpdateAsync(user)); await users.UpdateSecurityStampAsync(user); await QueueAccessChangedAsync(user, cancellationToken); await dbContext.SaveChangesAsync(cancellationToken); return 0; }, cancellationToken);

    private async Task SetRolesCoreAsync(ApplicationUser user, IReadOnlyCollection<string> roleNames, CancellationToken cancellationToken)
    { var currentStorageNames = await users.GetRolesAsync(user); if (currentStorageNames.Count > 0) EnsureSucceeded(await users.RemoveFromRolesAsync(user, currentStorageNames)); var requestedRoles = roleNames.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(); foreach (var displayName in requestedRoles) { var storageName = TenantRoleNameCodec.ToStorageName(user.TenantId, displayName); var normalizedStorageName = storageName.ToUpperInvariant(); var role = await roles.Roles.FirstOrDefaultAsync(x => x.TenantId == user.TenantId && x.NormalizedName == normalizedStorageName, cancellationToken); if (role is null) { role = new ApplicationRole { Id = Guid.NewGuid(), TenantId = user.TenantId, Name = storageName, Description = displayName }; EnsureSucceeded(await roles.CreateAsync(role)); } EnsureSucceeded(await users.AddToRoleAsync(user, storageName)); } }
    private async Task<ApplicationUser> GetEntityAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => await users.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == userId, cancellationToken) ?? throw new KeyNotFoundException("User not found.");
    private Task<ApplicationUser?> FindByTenantEmailAsync(Guid tenantId, string email, CancellationToken cancellationToken) { var normalizedEmail = email.Trim().ToUpperInvariant(); return users.Users.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.NormalizedEmail == normalizedEmail, cancellationToken); }
    private async Task<AccountResult> MapAsync(ApplicationUser user, CancellationToken cancellationToken) { var storageRoles = await users.GetRolesAsync(user); var roleNames = storageRoles.Select(x => TenantRoleNameCodec.ToDisplayName(user.TenantId, x)).ToArray(); var permissions = await dbContext.UserPermissions.AsNoTracking().Where(x => x.TenantId == user.TenantId && x.UserId == user.Id).Select(x => x.Permission).ToListAsync(cancellationToken); return new AccountResult(user.Id, user.TenantId, user.TenantSlug, user.Email ?? string.Empty, user.FirstName, user.LastName, user.IsActive, user.TwoFactorEnabled, roleNames, permissions); }
    private async Task QueueAccessChangedAsync(ApplicationUser user, CancellationToken cancellationToken) { var snapshot = await MapAsync(user, cancellationToken); outbox.Add(new UserAccessChangedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, user.TenantId, user.Id, user.IsActive, snapshot.Roles, snapshot.Permissions)); }

    private async Task<T> ExecuteInTransactionAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
    {
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            var result = await operation();
            await transaction.CommitAsync(cancellationToken);
            return result;
        });
    }

    private static void EnsureSucceeded(IdentityResult result) { if (result.Succeeded) return; throw new IdentityValidationException(result.Errors.GroupBy(x => string.IsNullOrWhiteSpace(x.Code) ? "identity" : x.Code).ToDictionary(x => x.Key, x => x.Select(y => y.Description).Distinct().ToArray())); }
}
