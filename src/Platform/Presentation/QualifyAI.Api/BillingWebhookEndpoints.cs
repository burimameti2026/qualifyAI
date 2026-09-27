using QualifyAI.Infrastructure;

namespace QualifyAI.Api;

public sealed record BillingWebhookRequest(string Provider, string EventId, string Type, Guid TenantId, string Status, DateTime OccurredAtUtc, Dictionary<string,string>? Data);

public static class BillingWebhookEndpoints
{
    public static IEndpointRouteBuilder MapBillingWebhooks(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/billing/webhooks/{provider}", async (string provider, BillingWebhookRequest request, BillingProviderRegistry registry, CancellationToken ct) =>
        {
            if (!string.Equals(provider, request.Provider, StringComparison.OrdinalIgnoreCase)) return Results.BadRequest(new { error = "Provider mismatch" });
            try
            {
                var adapter = registry.Get(provider);
                await adapter.HandleAsync(new BillingProviderEvent(provider, request.EventId, request.Type, request.TenantId, request.Status, request.OccurredAtUtc, request.Data), ct);
                return Results.Accepted();
            }
            catch (InvalidOperationException) { return Results.NotFound(new { error = "Billing provider not registered" }); }
        }).AllowAnonymous();
        return endpoints;
    }
}
