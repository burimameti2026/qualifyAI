using QualifyAI.Domain;

namespace QualifyAI.Infrastructure.Acquisition;

/// <summary>
/// Deterministic, non-network discovery data for the Renova demonstration workspace.
/// It is intentionally limited to .test domains and is never selected when a real SerpAPI
/// provider is configured.
/// </summary>
public sealed class RenovaDemoProspectDiscoveryProvider : IProspectDiscoveryProvider
{
    public string Name => "renova-demo";
    public bool IsConfigured => true;
    public string Description => "Safe deterministic Renova demo discovery using reserved .test domains.";

    public Task<IReadOnlyList<DiscoveryCandidate>> SearchAsync(IcpProfile icp, DiscoveryRunOptions options, CancellationToken ct = default)
    {
        IReadOnlyList<DiscoveryCandidate> candidates = new[]
        {
            new DiscoveryCandidate(
                "Balkan Build Supply (Demo)",
                "balkan-build.test",
                "https://balkan-build.test",
                "Demo company representing an Albanian building-material distributor evaluating facade products and supplier expansion.",
                icp.Industry,
                "AL"),
            new DiscoveryCandidate(
                "Adriatic Trade Materials (Demo)",
                "adriatic-trade.test",
                "https://adriatic-trade.test",
                "Demo company representing a construction-supply buyer expanding its facade-system assortment.",
                icp.Industry,
                "AL"),
            new DiscoveryCandidate(
                "Balkan Facade Partners (Demo)",
                "balkan-facade.test",
                "https://balkan-facade.test",
                "Demo facade contractor/distributor signal for the Renova Balkan acquisition scenario.",
                icp.Industry,
                "MK")
        };
        return Task.FromResult<IReadOnlyList<DiscoveryCandidate>>(candidates.Take(Math.Clamp(options.MaximumResults, 1, candidates.Count)).ToList());
    }
}
