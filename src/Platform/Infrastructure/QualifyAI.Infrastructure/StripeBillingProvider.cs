namespace QualifyAI.Infrastructure;

public sealed class StripeBillingProvider(IBillingEventProcessor processor) : IBillingProvider
{
    public string Name => "stripe";
    public Task HandleAsync(BillingProviderEvent item, CancellationToken ct = default) => processor.ProcessAsync(item, ct);
}
