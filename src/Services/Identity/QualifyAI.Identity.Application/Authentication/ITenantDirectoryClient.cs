namespace QualifyAI.Identity.Application.Authentication;
public interface ITenantDirectoryClient
{
    Task<TenantDirectoryEntry?> ResolveAsync(string slug, CancellationToken ct = default);
}
public sealed record TenantDirectoryEntry(Guid Id,string Slug,string Name,bool IsActive);
