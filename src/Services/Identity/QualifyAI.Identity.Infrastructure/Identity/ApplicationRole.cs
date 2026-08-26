using Microsoft.AspNetCore.Identity;
namespace QualifyAI.Identity.Infrastructure.Identity;
public sealed class ApplicationRole : IdentityRole<Guid>
{
    public Guid TenantId { get; set; }
    public string Description { get; set; } = "";
}
