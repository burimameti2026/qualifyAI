using System.Net;
using System.Net.Http.Json;
using QualifyAI.Identity.Application.Authentication;
namespace QualifyAI.Identity.Infrastructure.Tenants;
public sealed class TenantDirectoryClient(HttpClient client):ITenantDirectoryClient
{
    public async Task<TenantDirectoryEntry?> ResolveAsync(string slug,CancellationToken ct=default)
    {
        using var response=await client.GetAsync($"/tenants/by-slug/{Uri.EscapeDataString(slug)}",ct);
        if(response.StatusCode==HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TenantDirectoryEntry>(cancellationToken:ct);
    }
}
