using QualifyAI.Infrastructure;

namespace QualifyAI.Api;

public static class TenantLifecycleEndpoints
{
    public static IEndpointRouteBuilder MapTenantLifecycle(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/tenant-lifecycle/{tenantId:guid}", (Guid tenantId, int? take, ITenantLifecycleEventStore events) => Results.Ok(events.Get(tenantId, take ?? 100)));
        endpoints.MapGet("/api/tenant-alerts/{tenantId:guid}", (Guid tenantId, int? take, ITenantAlertService alerts) => Results.Ok(alerts.Get(tenantId, take ?? 100)));
        endpoints.MapPost("/api/tenant-alerts/{tenantId:guid}/{alertId:guid}/acknowledge", (Guid tenantId, Guid alertId, ITenantAlertService alerts) => { alerts.Acknowledge(tenantId, alertId); return Results.NoContent(); });
        return endpoints;
    }
}
