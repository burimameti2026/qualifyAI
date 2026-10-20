using Microsoft.AspNetCore.Authorization;
namespace LeadsAI.BuildingBlocks.Security.Authorization;
public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
