using Microsoft.AspNetCore.Authorization;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Claims;

namespace LeadsAI.BuildingBlocks.Security.Authorization;

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
