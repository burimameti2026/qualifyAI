using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using QualifyAI.BuildingBlocks.Security.Claims;
namespace QualifyAI.BuildingBlocks.Security.Tenancy;
public sealed class CurrentTenant(IHttpContextAccessor accessor) : ICurrentTenant
{
    public Guid Id
    {
        get
        {
            var raw = accessor.HttpContext?.User.FindFirstValue(QualifyAiClaimTypes.TenantId);
            return Guid.TryParse(raw, out var id) ? id : Guid.Empty;
        }
    }
    public string Slug => accessor.HttpContext?.User.FindFirstValue(QualifyAiClaimTypes.TenantSlug) ?? "";
    public bool IsResolved => Id != Guid.Empty;
}
