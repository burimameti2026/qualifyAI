using QualifyAI.Infrastructure;

namespace QualifyAI.Api;

public static class TenantLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapTenantLifecycle(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/tenant-lifecycle/{tenantId:guid}", (Guid tenantId, int? take, ITenantLifecycleEventStore events) => Results.Ok(events.Get(tenantId, take ?? 100)));
        return endpoints;
    }
}
