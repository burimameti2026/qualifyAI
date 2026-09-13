using Microsoft.AspNetCore.Authorization;
using LeadsAI.Application.Abstractions.Persistence;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.BuildingBlocks.Security.Claims;
using LeadsAI.BuildingBlocks.Security.Access;

namespace LeadsAI.Api.Security;

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
