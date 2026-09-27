namespace QualifyAI.Infrastructure;

public sealed class StripeBillingProvider(IBillingEventProcessor processor) : IBillingProviderAdapter
{
    public string Name => "stripe";

    public bool CanHandle(string eventType) =>
        eventType.StartsWith("customer.subscription", StringComparison.OrdinalIgnoreCase) ||
        eventType.StartsWith("invoice.", StringComparison.OrdinalIgnoreCase) ||
        eventType.StartsWith("payment_intent.", StringComparison.OrdinalIgnoreCase);

    public Task HandleAsync(BillingProviderEvent item, CancellationToken ct = default)
        => processor.ProcessAsync(item, ct);
}
