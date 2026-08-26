namespace QualifyAI.Identity.Application.Authentication;
public interface IAccountService
{
    Task<AccountResult> CreateUserAsync(CreateAccountRequest request,CancellationToken ct=default);
    Task<AccountResult?> GetUserAsync(Guid tenantId,Guid userId,CancellationToken ct=default);
    Task<IReadOnlyList<AccountResult>> ListUsersAsync(Guid tenantId,CancellationToken ct=default);
    Task SetRolesAsync(Guid tenantId,Guid userId,IReadOnlyCollection<string> roles,CancellationToken ct=default);
    Task SetPermissionsAsync(Guid tenantId,Guid userId,IReadOnlyCollection<string> permissions,CancellationToken ct=default);
    Task DisableAsync(Guid tenantId,Guid userId,CancellationToken ct=default);
    Task EnableAsync(Guid tenantId,Guid userId,CancellationToken ct=default);
    Task ChangePasswordAsync(Guid tenantId,Guid userId,string currentPassword,string newPassword,CancellationToken ct=default);
    Task<string> GeneratePasswordResetTokenAsync(Guid tenantId,string email,CancellationToken ct=default);
    Task ResetPasswordAsync(Guid tenantId,string email,string token,string newPassword,CancellationToken ct=default);
    Task<MfaSetupResult> BeginMfaAsync(Guid tenantId,Guid userId,CancellationToken ct=default);
    Task<bool> ConfirmMfaAsync(Guid tenantId,Guid userId,string code,CancellationToken ct=default);
    Task DisableMfaAsync(Guid tenantId,Guid userId,CancellationToken ct=default);
}
public sealed record CreateAccountRequest(Guid TenantId,string TenantSlug,string Email,string Password,string FirstName,string LastName,IReadOnlyCollection<string> Roles);
public sealed record AccountResult(Guid Id,Guid TenantId,string TenantSlug,string Email,string FirstName,string LastName,bool IsActive,bool TwoFactorEnabled,IReadOnlyCollection<string> Roles,IReadOnlyCollection<string> Permissions);
public sealed record MfaSetupResult(string SharedKey,string AuthenticatorUri);
