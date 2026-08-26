using Microsoft.AspNetCore.Authorization;
using QualifyAI.BuildingBlocks.Security.Claims;
namespace QualifyAI.BuildingBlocks.Security.Authorization;
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.Claims.Any(c => c.Type == QualifyAiClaimTypes.Permission &&
                                         string.Equals(c.Value, requirement.Permission, StringComparison.OrdinalIgnoreCase)))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
