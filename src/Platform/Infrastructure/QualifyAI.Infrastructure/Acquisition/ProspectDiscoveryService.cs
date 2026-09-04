using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

public sealed record DiscoveryRunOptions(
    string? Source = null,
    string? Region = null,
    int MaximumResults = 50,
    int MinimumScore = 70,
    string? TargetListName = null,
    bool CreateTargetList = true);

public sealed record DiscoveryProviderStatus(string Name, bool Configured, string Description);

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
    string Description
    {
        get;
    }
    Task<IReadOnlyList<DiscoveryCandidate>> SearchAsync(IcpProfile icp, DiscoveryRunOptions options, CancellationToken ct = default);
}

/// <summary>
/// Finds publicly indexed company websites through a customer-owned SerpAPI account.
/// It intentionally returns company-level evidence only; it does not fabricate contacts
/// or personal email addresses.
/// </summary>
public sealed class SerpApiProspectDiscoveryProvider(
    HttpClient http,
    IConfiguration configuration)
    : IProspectDiscoveryProvider
{
    private const string ApiKeyPath =
        "ProspectDiscovery:SerpApi:ApiKey";

    private const string MonthlyLimitPath =
        "ProspectDiscovery:SerpApi:MonthlySafetyLimit";

    private const string LimitPath =
        "ProspectDiscovery:SerpApi:Limit";

    public string Name => "serpapi";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(configuration[ApiKeyPath]);

    public string Description =>
        "Public company website discovery through SerpAPI.";

    public async Task<IReadOnlyList<DiscoveryCandidate>> SearchAsync(
        IcpProfile icp,
        DiscoveryRunOptions options,
        CancellationToken ct = default)
    {
        var apiKey = configuration[ApiKeyPath];

        if(string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "SerpAPI is not configured.");
        }

        // ------------------------------------------
        // 1. CHECK QUOTA BEFORE SEARCH
        // ------------------------------------------

        var account = await GetAccountUsageAsync(apiKey, ct);

        var safetyLimit =
            configuration.GetValue<int?>(MonthlyLimitPath)??200;

        // Never allow our configured limit to exceed
        // the actual SerpAPI plan limit.
        var effectiveLimit = Math.Min(
            safetyLimit,
            account.SearchesPerMonth);

        if(account.ThisMonthUsage>=effectiveLimit)
        {
            throw new InvalidOperationException(
                $"SERP monthly safety limit reached. "+
                $"Used: {account.ThisMonthUsage}, "+
                $"Application limit: {effectiveLimit}, "+
                $"Provider limit: {account.SearchesPerMonth}. "+
                $"Prospect discovery has been stopped.");
        }

        if(account.PlanSearchesLeft<=0)
        {
            throw new InvalidOperationException(
                "SerpAPI reports no searches remaining. "+
                "Prospect discovery has been stopped.");
        }

        // ------------------------------------------
        // 2. EXECUTE SEARCH
        // ------------------------------------------

        var query = BuildQuery(icp, options);

        var maximumResults =
            Math.Clamp(options.MaximumResults, 1, 100);

        var uri =
            $"search.json"+
            $"?engine=google"+
            $"&q={Uri.EscapeDataString(query)}"+
            $"&num={maximumResults}"+
            $"&api_key={Uri.EscapeDataString(apiKey)}";

        try
        {
            using var response = await http.GetAsync(uri, ct);

            var content = await response.Content.ReadAsStringAsync(ct);

            if(!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"SerpAPI search failed ({(int)response.StatusCode}).");
            }
            using var document =
            JsonDocument.Parse(content);

            if(!document.RootElement.TryGetProperty(
                    "organic_results",
                    out var organic)||
                organic.ValueKind!=JsonValueKind.Array)
            {
                return Array.Empty<DiscoveryCandidate>();
            }

            var candidates = new List<DiscoveryCandidate>();

            foreach(var result in organic.EnumerateArray())
            {
                var url = Read(result, "link");
                var domain = DomainFromUrl(url);

                if(string.IsNullOrWhiteSpace(url)||
                    string.IsNullOrWhiteSpace(domain)||
                    IsNonCompanyDomain(domain))
                {
                    continue;
                }

                var title = Read(result, "title");
                var snippet = Read(result, "snippet");
                var name = CompanyName(title, domain);

                candidates.Add(
                    new DiscoveryCandidate(
                        name,
                        domain,
                        url,
                        $"{title}. {snippet}".Trim(),
                        icp.Industry,
                        PrimaryCountry(icp.CountriesCsv)));
            }

            return candidates
                .GroupBy(
                    x => x.Domain,
                    StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .Take(maximumResults)
                .ToList();
        }
        catch(TaskCanceledException) when(!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException(
                "SerpAPI search timed out. Please try again.");
        }
        catch(HttpRequestException ex)
        {
            throw new InvalidOperationException(
                $"SerpAPI could not be reached: {ex.Message}", ex);
        }


    }

    // ------------------------------------------
    // SERP ACCOUNT / QUOTA
    // ------------------------------------------

    private async Task<SerpApiAccountUsage> GetAccountUsageAsync(
        string apiKey,
        CancellationToken ct)
    {
        var uri =
            $"account.json"+
            $"?api_key={Uri.EscapeDataString(apiKey)}";

        using var response =
            await http.GetAsync(uri, ct);

        var json =
            await response.Content.ReadAsStringAsync(ct);

        if(!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                "Unable to verify SerpAPI quota. "+
                "Search was blocked for safety.");
        }

        using var document = JsonDocument.Parse(json);

        var root = document.RootElement;

        return new SerpApiAccountUsage
        {
            PlanName=GetString(root, "plan_name"),

            SearchesPerMonth=
                GetInt(root, "searches_per_month"),

            PlanSearchesLeft=
                GetInt(root, "plan_searches_left"),

            ThisMonthUsage=
                GetInt(root, "this_month_usage"),

            ThisHourSearches=
                GetInt(root, "this_hour_searches"),

            AccountRateLimitPerHour=
                GetInt(root, "account_rate_limit_per_hour")
        };
    }

    private static int GetInt(
        JsonElement root,
        string property)
    {
        return root.TryGetProperty(property, out var value)&&
               value.TryGetInt32(out var result)
            ? result
            : 0;
    }

    private static string GetString(
        JsonElement root,
        string property)
    {
        return root.TryGetProperty(property, out var value)&&
               value.ValueKind==JsonValueKind.String
            ? value.GetString()??string.Empty
            : string.Empty;
    }

    private static string BuildQuery(
        IcpProfile icp,
        DiscoveryRunOptions options)
    {
        var parts = new[]
            {
                icp.Industry,
                options.Region,
                icp.CountriesCsv,
                icp.IntentKeywordsCsv
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim());

        return string.Join(" ", parts);
    }

    private static string Read(
        JsonElement item,
        string property) =>
        item.TryGetProperty(property, out var value)&&
        value.ValueKind==JsonValueKind.String
            ? value.GetString()??string.Empty
            : string.Empty;

    private static string DomainFromUrl(string value)
    {
        if(!Uri.TryCreate(
                value,
                UriKind.Absolute,
                out var uri))
            return string.Empty;

        return uri.Host
            .Trim()
            .ToLowerInvariant()
            .TrimStart('.')
            .Replace(
                "www.",
                string.Empty,
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonCompanyDomain(string domain) =>
        domain.EndsWith(
            "google.com",
            StringComparison.OrdinalIgnoreCase)
        ||domain.EndsWith(
            "linkedin.com",
            StringComparison.OrdinalIgnoreCase)
        ||domain.EndsWith(
            "facebook.com",
            StringComparison.OrdinalIgnoreCase)
        ||domain.EndsWith(
            "instagram.com",
            StringComparison.OrdinalIgnoreCase)
        ||domain.EndsWith(
            "wikipedia.org",
            StringComparison.OrdinalIgnoreCase);

    private static string CompanyName(
        string title,
        string domain)
    {
        var cleaned = Regex.Replace(
            title,
            @"\s+[|–—-]\s+.*$",
            string.Empty).Trim();

        return string.IsNullOrWhiteSpace(cleaned)
            ? domain.Split('.')[0]
            : cleaned;
    }

    private static string PrimaryCountry(
        string countriesCsv) =>
        countriesCsv
            .Split(
                ',',
                StringSplitOptions.RemoveEmptyEntries|
                StringSplitOptions.TrimEntries)
            .FirstOrDefault()??string.Empty;
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

    public async Task<ProspectDiscoveryResult> DiscoverAsync(Guid tenantId, Guid icpId, DiscoveryRunOptions options, CancellationToken ct = default)
    {
        var icp = await db.IcpProfiles.FirstOrDefaultAsync(x => x.TenantId==tenantId&&x.Id==icpId&&x.Active, ct)
            ??throw new InvalidOperationException("Select an active ideal customer profile before discovery.");
        var providerName = string.IsNullOrWhiteSpace(options.Source) ? "serpapi" : options.Source.Trim();
        var provider = providers.FirstOrDefault(x => string.Equals(x.Name, providerName, StringComparison.OrdinalIgnoreCase))
            ??throw new InvalidOperationException($"Discovery provider '{providerName}' is not available.");
        if(!provider.IsConfigured)
            throw new InvalidOperationException($"Discovery provider '{provider.Name}' is not configured. {provider.Description}");

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