using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace QualifyAI.BuildingBlocks.Security.Authorization;

public static class AccessPolicyNames
{
    public const string PermissionPrefix = "qai:permission:";
    public const string ModulePrefix = "qai:module:";
}

public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = AccessPolicyNames.PermissionPrefix + permission;
    }

    public string Permission { get; }
}

public sealed class RequireModuleAttribute : AuthorizeAttribute
{
    public RequireModuleAttribute(string module)
    {
        Module = module;
        Policy = AccessPolicyNames.ModulePrefix + module;
    }

    public string Module { get; }
}

public sealed class ModuleRequirement(string module) : IAuthorizationRequirement
{
    public string Module { get; } = module;
}

public sealed class QualifyAiAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(AccessPolicyNames.PermissionPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var permission = policyName[AccessPolicyNames.PermissionPrefix.Length..];
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build());
        }

        if (policyName.StartsWith(AccessPolicyNames.ModulePrefix, StringComparison.OrdinalIgnoreCase))
        {
            var module = policyName[AccessPolicyNames.ModulePrefix.Length..];
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new ModuleRequirement(module))
                .Build());
        }

        return base.GetPolicyAsync(policyName);
    }
}
