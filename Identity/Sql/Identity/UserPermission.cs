namespace QualifyAI.Identity.Persistence.SqlServer.Identity;
public sealed class UserPermission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public string Permission { get; set; } = "";
}
