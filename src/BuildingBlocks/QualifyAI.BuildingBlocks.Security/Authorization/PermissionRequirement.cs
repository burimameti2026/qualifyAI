using Microsoft.AspNetCore.Authorization;
namespace QualifyAI.BuildingBlocks.Security.Authorization;
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
