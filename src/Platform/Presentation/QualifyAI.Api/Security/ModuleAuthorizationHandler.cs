using Microsoft.AspNetCore.Authorization;
using QualifyAI.Application.Abstractions.Persistence;
using QualifyAI.BuildingBlocks.Security.Authorization;
using QualifyAI.BuildingBlocks.Security.Claims;
using QualifyAI.BuildingBlocks.Security.Access;

namespace QualifyAI.Api.Security;

public sealed class ModuleAuthorizationHandler(ITenantEntitlementRepository entitlements)
    : AuthorizationHandler<ModuleRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ModuleRequirement requirement)
    {
        if (context.User.FindAll(QualifyAiClaimTypes.Permission)
            .Any(x => x.Value.Equals(QualifyAiPermissions.SystemAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return;
        }

        var tenantValue = context.User.FindFirst(QualifyAiClaimTypes.TenantId)?.Value;
        if (!Guid.TryParse(tenantValue, out var tenantId))
            return;

        var snapshot = await entitlements.GetAsync(tenantId);
        if (snapshot is not null && snapshot.IsAccessibleAt(DateTime.UtcNow) && snapshot.HasModule(requirement.Module))
            context.Succeed(requirement);
    }
}
