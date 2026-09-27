using Microsoft.AspNetCore.Authorization;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.BuildingBlocks.Security.Claims;

namespace QualifyAI.BuildingBlocks.Security.Authorization;

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var granted = context.User.Claims
            .Where(c => c.Type == QualifyAiClaimTypes.Permission)
            .Select(c => c.Value)
            .Any(value =>
                string.Equals(value, QualifyAiPermissions.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, requirement.Permission, StringComparison.OrdinalIgnoreCase));

        if (granted)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
