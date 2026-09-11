using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Application;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Infrastructure.Acquisition;

/// <summary>
/// Tenant-isolated SerpAPI discovery provider. Credentials and safety limits are read
/// only from the currently resolved tenant database; process-level SerpAPI credentials
/// are deliberately not used for acquisition runs.
/// </summary>
public sealed class TenantSerpApiProspectDiscoveryProvider(
    HttpClient http,
    AppDbContext db,
    ITenantContext tenant) : IProspectDiscoveryProvider
{
    private const string ApiKeySetting = "acquisition.serpapi.apiKey";
    private const string MonthlyLimitSetting = "acquisition.serpapi.monthlySafetyLimit";

    public string Name => "serpapi";

    public bool IsConfigured =>
        tenant.Current is not null &&
        db.TenantSettings.Any(x => x.TenantId == tenant.Current.Id && x.Key == ApiKeySetting && !string.IsNullOrWhiteSpace(x.Value));

    public string Description => "Public company website discovery through the tenant's SerpAPI account.";

    public async Task<IReadOnlyList<DiscoveryCandidate>> SearchAsync(IcpProfile icp, DiscoveryRunOptions options, CancellationToken ct = default)
    {
        var tenantId = tenant.Current?.Id ?? Guid.Empty;
        if (tenantId == Guid.Empty) throw new InvalidOperationException("Tenant context is required for SerpAPI discovery.");

        var settings = await db.TenantSettings
            .Where(x => x.TenantId == tenantId && (x.Key == ApiKeySetting || x.Key == MonthlyLimitSetting))
            .ToDictionaryAsync(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase, ct);

        if (!settings.TryGetValue(ApiKeySetting, out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("SerpAPI is not configured for this tenant.");

        var account = await GetAccountUsageAsync(apiKey, ct);
        var configuredLimit = 200;
        if (settings.TryGetValue(MonthlyLimitSetting, out var limitValue) && int.TryParse(limitValue, out var parsedLimit))
            configuredLimit = Math.Clamp(parsedLimit, 1, 100000);

        var effectiveLimit = Math.Min(configuredLimit, account.SearchesPerMonth);
        if (account.ThisMonthUsage >= effectiveLimit)
            throw new InvalidOperationException($"SerpAPI monthly safety limit reached for this tenant ({account.ThisMonthUsage}/{effectiveLimit}).");
        if (account.PlanSearchesLeft <= 0)
            throw new InvalidOperationException("SerpAPI reports no searches remaining for this tenant.");

        var query = BuildQuery(icp, options);
        var maximumResults = Math.Clamp(options.MaximumResults, 1, 100);
        var uri = $"search.json?engine=google&q={Uri.EscapeDataString(query)}&num={maximumResults}&api_key={Uri.EscapeDataString(apiKey)}";

        try
        {
            using var response = await http.GetAsync(uri, ct);
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
                throw new InvalidOperationException($"SerpAPI search failed ({(int)response.StatusCode}).");

            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("organic_results", out var organic) || organic.ValueKind != JsonValueKind.Array)
                return Array.Empty<DiscoveryCandidate>();

            return organic.EnumerateArray()
                .Select(result =>
                {
                    var url = Read(result, "link");
                    var domain = DomainFromUrl(url);
                    var title = Read(result, "title");
                    var snippet = Read(result, "snippet");
                    return new DiscoveryCandidate(
                        CompanyName(title, domain), domain, url, $"{title}. {snippet}".Trim(), icp.Industry, PrimaryCountry(icp.CountriesCsv));
                })
                .Where(x => !string.IsNullOrWhiteSpace(x.Domain) && !IsNonCompanyDomain(x.Domain))
                .GroupBy(x => x.Domain, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .Take(maximumResults)
                .ToList();
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new InvalidOperationException("SerpAPI search timed out for this tenant.");
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"SerpAPI could not be reached: {ex.Message}", ex);
        }
    }

    private async Task<SerpApiAccountUsage> GetAccountUsageAsync(string apiKey, CancellationToken ct)
    {
        using var response = await http.GetAsync($"account.json?api_key={Uri.EscapeDataString(apiKey)}", ct);
        var json = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException("Unable to verify this tenant's SerpAPI quota. Search was blocked for safety.");

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return new SerpApiAccountUsage
        {
            PlanName = GetString(root, "plan_name"),
            SearchesPerMonth = GetInt(root, "searches_per_month"),
            PlanSearchesLeft = GetInt(root, "plan_searches_left"),
            ThisMonthUsage = GetInt(root, "this_month_usage"),
            ThisHourSearches = GetInt(root, "this_hour_searches"),
            AccountRateLimitPerHour = GetInt(root, "account_rate_limit_per_hour")
        };
    }

    private static int GetInt(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.TryGetInt32(out var result) ? result : 0;

    private static string GetString(JsonElement root, string property) =>
        root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string BuildQuery(IcpProfile icp, DiscoveryRunOptions options) =>
        string.Join(" ", new[] { icp.Industry, options.Region, icp.CountriesCsv, icp.IntentKeywordsCsv }
            .Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));

    private static string Read(JsonElement item, string property) =>
        item.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string DomainFromUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return string.Empty;
        return uri.Host.Trim().ToLowerInvariant().TrimStart('.').Replace("www.", string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsNonCompanyDomain(string domain) =>
        domain.EndsWith("google.com", StringComparison.OrdinalIgnoreCase) ||
        domain.EndsWith("linkedin.com", StringComparison.OrdinalIgnoreCase) ||
        domain.EndsWith("facebook.com", StringComparison.OrdinalIgnoreCase) ||
        domain.EndsWith("instagram.com", StringComparison.OrdinalIgnoreCase) ||
        domain.EndsWith("wikipedia.org", StringComparison.OrdinalIgnoreCase);

    private static string CompanyName(string title, string domain)
    {
        var cleaned = Regex.Replace(title, @"\s+[|–—-]\s+.*$", string.Empty).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? domain.Split('.')[0] : cleaned;
    }

    private static string PrimaryCountry(string countriesCsv) =>
        countriesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;
}