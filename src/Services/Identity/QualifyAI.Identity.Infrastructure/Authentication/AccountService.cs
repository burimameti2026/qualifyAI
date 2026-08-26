using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Identity.Application.Authentication;
using QualifyAI.Identity.Infrastructure.Identity;
using QualifyAI.Identity.Infrastructure.Persistence;

namespace QualifyAI.Identity.Infrastructure.Authentication;

public sealed class AccountService(UserManager<ApplicationUser> users,RoleManager<ApplicationRole> roles,IdentityDbContext db):IAccountService
{
    public async Task<AccountResult>CreateUserAsync(CreateAccountRequest request,CancellationToken ct=default)
    {
        var existing=await users.Users.FirstOrDefaultAsync(x=>x.TenantId==request.TenantId&&x.NormalizedEmail==request.Email.ToUpper(),ct);
        if(existing is not null)throw new InvalidOperationException("User already exists.");
        var user=new ApplicationUser{Id=Guid.NewGuid(),TenantId=request.TenantId,TenantSlug=request.TenantSlug,UserName=request.Email.Trim().ToLowerInvariant(),Email=request.Email.Trim().ToLowerInvariant(),FirstName=request.FirstName.Trim(),LastName=request.LastName.Trim(),EmailConfirmed=true,IsActive=true};
        var created=await users.CreateAsync(user,request.Password);if(!created.Succeeded)throw new InvalidOperationException(string.Join("; ",created.Errors.Select(x=>x.Description)));
        await SetRolesAsync(request.TenantId,user.Id,request.Roles,ct);return await MapAsync(user,ct);
    }
    public async Task<AccountResult?>GetUserAsync(Guid tenantId,Guid userId,CancellationToken ct=default){var u=await users.Users.FirstOrDefaultAsync(x=>x.TenantId==tenantId&&x.Id==userId,ct);return u is null?null:await MapAsync(u,ct);}
    public async Task<IReadOnlyList<AccountResult>>ListUsersAsync(Guid tenantId,CancellationToken ct=default){var list=await users.Users.Where(x=>x.TenantId==tenantId).OrderBy(x=>x.Email).ToListAsync(ct);var result=new List<AccountResult>();foreach(var u in list)result.Add(await MapAsync(u,ct));return result;}
    public async Task SetRolesAsync(Guid tenantId,Guid userId,IReadOnlyCollection<string> roleNames,CancellationToken ct=default){var user=await GetEntity(tenantId,userId,ct);var current=await users.GetRolesAsync(user);if(current.Count>0)await users.RemoveFromRolesAsync(user,current);foreach(var roleName in roleNames.Distinct(StringComparer.OrdinalIgnoreCase)){var role=await roles.Roles.FirstOrDefaultAsync(x=>x.TenantId==tenantId&&x.NormalizedName==roleName.ToUpper(),ct);if(role is null){role=new ApplicationRole{Id=Guid.NewGuid(),TenantId=tenantId,Name=roleName};var rr=await roles.CreateAsync(role);if(!rr.Succeeded)throw new InvalidOperationException(string.Join("; ",rr.Errors.Select(x=>x.Description)));}await users.AddToRoleAsync(user,roleName);}await users.UpdateSecurityStampAsync(user);}
    public async Task SetPermissionsAsync(Guid tenantId,Guid userId,IReadOnlyCollection<string> permissions,CancellationToken ct=default){_ = await GetEntity(tenantId,userId,ct);var existing=await db.UserPermissions.Where(x=>x.TenantId==tenantId&&x.UserId==userId).ToListAsync(ct);db.UserPermissions.RemoveRange(existing);db.UserPermissions.AddRange(permissions.Distinct(StringComparer.OrdinalIgnoreCase).Select(p=>new UserPermission{TenantId=tenantId,UserId=userId,Permission=p}));await db.SaveChangesAsync(ct);}
    public Task DisableAsync(Guid tenantId,Guid userId,CancellationToken ct=default)=>SetActiveAsync(tenantId,userId,false,ct);
    public Task EnableAsync(Guid tenantId,Guid userId,CancellationToken ct=default)=>SetActiveAsync(tenantId,userId,true,ct);
    private async Task SetActiveAsync(Guid t,Guid id,bool active,CancellationToken ct){var u=await GetEntity(t,id,ct);u.IsActive=active;await users.UpdateAsync(u);await users.UpdateSecurityStampAsync(u);}
    public async Task ChangePasswordAsync(Guid t,Guid id,string currentPassword,string newPassword,CancellationToken ct=default){var u=await GetEntity(t,id,ct);var r=await users.ChangePasswordAsync(u,currentPassword,newPassword);if(!r.Succeeded)throw new InvalidOperationException(string.Join("; ",r.Errors.Select(x=>x.Description)));}
    public async Task<string>GeneratePasswordResetTokenAsync(Guid t,string email,CancellationToken ct=default){var normalized=email.Trim().ToUpperInvariant();var u=await users.Users.FirstOrDefaultAsync(x=>x.TenantId==t&&x.NormalizedEmail==normalized,ct)??throw new KeyNotFoundException("User not found.");return await users.GeneratePasswordResetTokenAsync(u);}
    public async Task ResetPasswordAsync(Guid t,string email,string token,string newPassword,CancellationToken ct=default){var normalized=email.Trim().ToUpperInvariant();var u=await users.Users.FirstOrDefaultAsync(x=>x.TenantId==t&&x.NormalizedEmail==normalized,ct)??throw new KeyNotFoundException("User not found.");var r=await users.ResetPasswordAsync(u,token,newPassword);if(!r.Succeeded)throw new InvalidOperationException(string.Join("; ",r.Errors.Select(x=>x.Description)));}
    public async Task<MfaSetupResult>BeginMfaAsync(Guid t,Guid id,CancellationToken ct=default){var u=await GetEntity(t,id,ct);var key=await users.GetAuthenticatorKeyAsync(u);if(string.IsNullOrWhiteSpace(key)){await users.ResetAuthenticatorKeyAsync(u);key=await users.GetAuthenticatorKeyAsync(u);}var uri=$"otpauth://totp/QualifyAI:{Uri.EscapeDataString(u.Email??u.UserName??u.Id.ToString())}?secret={key}&issuer=QualifyAI&digits=6";return new(key??"",uri);}
    public async Task<bool>ConfirmMfaAsync(Guid t,Guid id,string code,CancellationToken ct=default){var u=await GetEntity(t,id,ct);var valid=await users.VerifyTwoFactorTokenAsync(u,TokenOptions.DefaultAuthenticatorProvider,code.Replace(" ","").Replace("-",""));if(valid)await users.SetTwoFactorEnabledAsync(u,true);return valid;}
    public async Task DisableMfaAsync(Guid t,Guid id,CancellationToken ct=default){var u=await GetEntity(t,id,ct);await users.SetTwoFactorEnabledAsync(u,false);await users.ResetAuthenticatorKeyAsync(u);}
    private async Task<ApplicationUser>GetEntity(Guid t,Guid id,CancellationToken ct)=>await users.Users.FirstOrDefaultAsync(x=>x.TenantId==t&&x.Id==id,ct)??throw new KeyNotFoundException("User not found.");
    private async Task<AccountResult>MapAsync(ApplicationUser u,CancellationToken ct){var roleNames=await users.GetRolesAsync(u);var permissions=await db.UserPermissions.AsNoTracking().Where(x=>x.TenantId==u.TenantId&&x.UserId==u.Id).Select(x=>x.Permission).ToListAsync(ct);return new(u.Id,u.TenantId,u.TenantSlug,u.Email??"",u.FirstName,u.LastName,u.IsActive,u.TwoFactorEnabled,roleNames.ToArray(),permissions);}
}
