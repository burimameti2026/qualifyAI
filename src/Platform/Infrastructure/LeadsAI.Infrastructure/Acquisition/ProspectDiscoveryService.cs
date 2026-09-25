using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using LeadsAI.Domain;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Infrastructure.Acquisition;

public sealed record DiscoveryRunOptions(
    string? Source = null,
    string? Region = null,
    int MaximumResults = 50,
    int MinimumScore = 70,
    string? TargetListName = null,
    bool CreateTargetList = true,
    Guid? TenantId = null);

public sealed record DiscoveryProviderStatus(string Name, bool Configured, string Description);

public sealed record DiscoveryVerificationResult(
    bool Verified,
    string? Error,
    string? PlanName = null,
    int? PlanSearchesLeft = null,
    int? ThisMonthUsage = null);

public sealed record DiscoveryCandidate(
    string CompanyName,
    string Domain,
    string SourceUrl,
    string Evidence,
    string? Industry = null,
    string? Country = null);

public sealed record ProspectDiscoveryResult(
    string Provider,
    int Received,
    int Created,
    int Updated,
    int Qualified,
    int Duplicates,
    int Rejected,
    Guid? TargetListId,
    string? TargetListName,
    DateTime CompletedAtUtc);

public interface IProspectDiscoveryProvider
{
    string Name
    {
        get;
    }
    bool IsConfigured
    {
        get;
    }
    Task<bool> IsConfiguredForTenantAsync(Guid? tenantId, CancellationToken ct = default);
    Task<DiscoveryVerificationResult> VerifyAsync(Guid? tenantId = null, CancellationToken ct = default);
    string Description
    {
        get;
    }
    Task<IReadOnlyList<DiscoveryCandidate>> SearchAsync(IcpProfile icp, DiscoveryRunOptions options, CancellationToken ct = default);
}

public sealed class SerpApiAccountUsage
{
    public string PlanName { get; init; } = string.Empty;

    public int SearchesPerMonth
    {
        get; init;
    }

    public int PlanSearchesLeft
    {
        get; init;
    }

    public int ThisMonthUsage
    {
        get; init;
    }

    public int ThisHourSearches
    {
        get; init;
    }

    public int AccountRateLimitPerHour
    {
        get; init;
    }
}

public sealed class ProspectDiscoveryService(AppDbContext db, IEnumerable<IProspectDiscoveryProvider> providers)
{
    public IReadOnlyList<DiscoveryProviderStatus> ProviderStatus() => providers
        .Select(x => new DiscoveryProviderStatus(x.Name, x.IsConfigured, x.Description))
        .OrderBy(x => x.Name)
        .ToList();

    public async Task<DiscoveryVerificationResult> VerifyProviderAsync(
        string name,
        Guid tenantId,
        CancellationToken ct = default)
    {
        var provider = providers.FirstOrDefault(x =>
            string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Discovery provider '{name}' is not available.");

        return await provider.VerifyAsync(tenantId, ct);
    }
    public async Task<ProspectDiscoveryResult> DiscoverAsync(Guid tenantId, Guid icpId, DiscoveryRunOptions options, CancellationToken ct = default)
    {
        var icp = await db.IcpProfiles.FirstOrDefaultAsync(x => x.TenantId==tenantId&&x.Id==icpId&&x.Active, ct)
            ??throw new InvalidOperationException("Select an active ideal customer profile before discovery.");
        var providerName = string.IsNullOrWhiteSpace(options.Source) ? "serpapi" : options.Source.Trim();
        var provider = providers.FirstOrDefault(x => string.Equals(x.Name, providerName, StringComparison.OrdinalIgnoreCase))
            ??throw new InvalidOperationException($"Discovery provider '{providerName}' is not available.");
        if(!provider.IsConfigured)
            throw new InvalidOperationException($"Discovery provider '{provider.Name}' is not configured. {provider.Description}");

        var verification = await provider.VerifyAsync(tenantId, ct);
        if(!verification.Verified)
            throw new InvalidOperationException(
                verification.Error ?? $"Discovery provider '{provider.Name}' could not be verified.");

        var candidates = await provider.SearchAsync(icp, options, ct);
        var existing = await db.Prospects.Where(x => x.TenantId==tenantId).ToListAsync(ct);
        var byDomain = existing.Where(x => !string.IsNullOrWhiteSpace(x.Domain))
            .GroupBy(x => NormalizeDomain(x.Domain), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var qualified = new List<Prospect>();
        var created = 0;
        var updated = 0;
        var duplicates = 0;
        var rejected = 0;
        var now = DateTime.UtcNow;

        foreach(var candidate in candidates)
        {
            var domain = NormalizeDomain(candidate.Domain);
            if(string.IsNullOrWhiteSpace(domain)||string.IsNullOrWhiteSpace(candidate.CompanyName)) { rejected++; continue; }
            var score = Score(icp, candidate, options.Region);
            if(score.Priority<Math.Clamp(options.MinimumScore, 0, 100)) { rejected++; continue; }

            if(!byDomain.TryGetValue(domain, out var prospect))
            {
                prospect=new Prospect
                {
                    TenantId=tenantId,
                    CompanyName=candidate.CompanyName.Trim(),
                    Domain=domain,
                    Industry=candidate.Industry?.Trim()??string.Empty,
                    Country=candidate.Country?.Trim()??string.Empty,
                    Source=provider.Name,
                    SourceUrl=candidate.SourceUrl,
                    DatasetOrigin="public-web-discovery",
                    VerificationStatus="public-source",
                    ContactReadiness="company-only",
                    SizeBand="unknown",
                    SuggestedBuyer="Needs enrichment",
                    Priority=score.Priority>=85 ? "high" : "medium",
                    OutreachStatus="not-ready",
                    CreatedAtUtc=now,
                    UpdatedAtUtc=now
                };
                db.Prospects.Add(prospect);
                byDomain[domain]=prospect;
                created++;
            }
            else
            {
                if(qualified.Any(x => x.Id==prospect.Id)) { duplicates++; continue; }
                prospect.CompanyName=Prefer(candidate.CompanyName, prospect.CompanyName);
                prospect.SourceUrl=Prefer(candidate.SourceUrl, prospect.SourceUrl);
                prospect.Source=provider.Name;
                prospect.DatasetOrigin="public-web-discovery";
                prospect.VerificationStatus="public-source";
                prospect.UpdatedAtUtc=now;
                updated++;
            }

            prospect.Evaluate(score.Fit, score.Intent);
            prospect.PainHypothesis=score.Reason;
            db.ProspectSignals.Add(new ProspectSignal
            {
                TenantId=tenantId,
                ProspectId=prospect.Id,
                Type="public-web-match",
                Source=provider.Name,
                Evidence=Trim(candidate.Evidence, 1800),
                Score=score.Intent,
                SourceUrl=candidate.SourceUrl,
                ObservedAtUtc=now
            });
            qualified.Add(prospect);
        }

        TargetList? targetList = null;
        if(options.CreateTargetList&&qualified.Count>0)
        {
            targetList=new TargetList
            {
                TenantId=tenantId,
                IcpProfileId=icp.Id,
                Name=string.IsNullOrWhiteSpace(options.TargetListName) ? $"Review — {icp.Name} — {now:yyyy-MM-dd}" : options.TargetListName.Trim(),
                Description=$"Online discovery via {provider.Name}. {qualified.Count} accounts met score ≥ {Math.Clamp(options.MinimumScore, 0, 100)}. Human review required before outreach.",
                Dynamic=false
            };
            db.TargetLists.Add(targetList);
            db.TargetListMembers.AddRange(qualified.Select(x => new TargetListMember { TenantId=tenantId, TargetListId=targetList.Id, ProspectId=x.Id, AddedAtUtc=now }));
        }
        icp.LastDiscoveryAtUtc=now;
        await db.SaveChangesAsync(ct);

        return new ProspectDiscoveryResult(provider.Name, candidates.Count, created, updated, qualified.Count, duplicates, rejected,
            targetList?.Id, targetList?.Name, now);
    }

    private static (int Fit, int Intent, int Priority, string Reason) Score(IcpProfile icp, DiscoveryCandidate candidate, string? region)
    {
        var evidence = $"{candidate.CompanyName} {candidate.Domain} {candidate.Evidence}".ToLowerInvariant();
        var industries = Tokens(icp.Industry);
        var countries = Tokens(icp.CountriesCsv);
        var intent = Tokens(icp.IntentKeywordsCsv);
        var regions = Tokens(region);
        var industryHits = industries.Count==0 ? 1 : industries.Count(x => evidence.Contains(x, StringComparison.OrdinalIgnoreCase));
        var countryHits = countries.Count==0 ? 1 : countries.Count(x => evidence.Contains(x, StringComparison.OrdinalIgnoreCase));
        var regionHits = regions.Count==0 ? 0 : regions.Count(x => evidence.Contains(x, StringComparison.OrdinalIgnoreCase));
        var intentHits = intent.Count(x => evidence.Contains(x, StringComparison.OrdinalIgnoreCase));
        var fit = Math.Clamp(45+industryHits*20+countryHits*15+regionHits*10, 0, 100);
        var intentScore = Math.Clamp(35+intentHits*22+(regionHits>0 ? 8 : 0), 0, 100);
        var priority = (int)Math.Round(fit*.55m+intentScore*.45m);
        var reason = $"Public-web match: industry {industryHits}/{Math.Max(1, industries.Count)}, market {countryHits+regionHits}/{Math.Max(1, countries.Count+regions.Count)}, intent {intentHits}/{Math.Max(1, intent.Count)}.";
        return (fit, intentScore, priority, reason);
    }

    private static List<string> Tokens(string? value) => (value??string.Empty).Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
        .Where(x => x.Length>=3).Select(x => x.ToLowerInvariant()).Distinct().ToList();
    private static string NormalizeDomain(string value) => value.Trim().ToLowerInvariant().Replace("https://", string.Empty).Replace("http://", string.Empty).Replace("www.", string.Empty).Split('/')[0].TrimEnd('.');
    private static string Prefer(string incoming, string fallback) => string.IsNullOrWhiteSpace(incoming) ? fallback : incoming.Trim();
    private static string Trim(string value, int max) => value.Length<=max ? value : value[..max];
}