using System.Net;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using LeadsAI.Domain;

namespace LeadsAI.Infrastructure.Acquisition;

public sealed record ProspectEnrichmentEvidence(
    string Field,
    string Value,
    string SourceUrl,
    string Evidence,
    DateTime ObservedAtUtc);

public sealed record ProspectEnrichmentResult(
    string Provider,
    IReadOnlyList<ProspectEnrichmentEvidence> Evidence,
    string? Warning = null);

public interface IProspectEnrichmentProvider
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<ProspectEnrichmentResult> EnrichAsync(Prospect prospect, CancellationToken ct = default);
}

/// <summary>
/// Extracts only explicitly published company-level contact channels from the prospect's
/// public website. It does not infer a person's identity or manufacture an email address.
/// </summary>
public sealed class PublicWebsiteProspectEnrichmentProvider(
    HttpClient http,
    IConfiguration configuration) : IProspectEnrichmentProvider
{
    private static readonly Regex EmailPattern = new(
        @"(?<![\w.+-])[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}(?![\w.-])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex PhonePattern = new(
        @"(?<!\w)(?:\+?\d[\d .()/-]{7,}\d)(?!\w)",
        RegexOptions.Compiled);
    private static readonly Regex HtmlTagPattern = new("<[^>]+>", RegexOptions.Compiled);
    private static readonly Regex EmailNoisePattern = new(
        @"(example\.com|example\.org|wixpress\.com|sentry\.io|cloudflare\.com|schema\.org)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string Name => "public-website";
    public bool IsConfigured => configuration.GetValue("ProspectEnrichment:PublicWebsite:Enabled", true);

    public async Task<ProspectEnrichmentResult> EnrichAsync(Prospect prospect, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new(Name, Array.Empty<ProspectEnrichmentEvidence>(), "Public website enrichment is disabled.");

        if (!Uri.TryCreate(prospect.SourceUrl, UriKind.Absolute, out var sourceUri) ||
            sourceUri.Scheme is not ("http" or "https"))
        {
            var domain = prospect.Domain.Trim();
            if (string.IsNullOrWhiteSpace(domain) ||
                !Uri.TryCreate("https://" + domain, UriKind.Absolute, out sourceUri))
                return new(Name, Array.Empty<ProspectEnrichmentEvidence>(), "No valid public company website is available.");
        }

        var evidence = new List<ProspectEnrichmentEvidence>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urls = new[]
        {
            sourceUri,
            new Uri(sourceUri, "/contact"),
            new Uri(sourceUri, "/contact-us"),
            new Uri(sourceUri, "/about")
        };

        foreach (var url in urls)
        {
            if (!string.Equals(url.Host, sourceUri.Host, StringComparison.OrdinalIgnoreCase) ||
                !visited.Add(url.AbsoluteUri))
                continue;

            ct.ThrowIfCancellationRequested();
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("LeadsAI-ResearchBot/1.0 (+public company contact verification)");
                using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
                if (!response.IsSuccessStatusCode ||
                    response.Content.Headers.ContentType?.MediaType is not ("text/html" or "application/xhtml+xml"))
                    continue;

                var html = await response.Content.ReadAsStringAsync(ct);
                if (html.Length > 1_500_000)
                    html = html[..1_500_000];

                var text = WebUtility.HtmlDecode(HtmlTagPattern.Replace(
                    Regex.Replace(html, @"(?is)<(script|style|noscript)[^>]*>.*?</\1>", " "),
                    " "));
                var observed = DateTime.UtcNow;

                foreach (Match match in EmailPattern.Matches(WebUtility.HtmlDecode(html)))
                {
                    var email = match.Value.Trim().TrimEnd('.', ',', ';', ':').ToLowerInvariant();
                    var domain = email.Split('@').LastOrDefault() ?? string.Empty;
                    if (EmailNoisePattern.IsMatch(domain) ||
                        email.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                        email.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
                        continue;
                    AddUnique(evidence, "public-email", email, url, "Email address explicitly published on the company website.", observed);
                }

                foreach (Match match in PhonePattern.Matches(text))
                {
                    var phone = Regex.Replace(match.Value.Trim(), @"\s+", " ");
                    var digits = phone.Count(char.IsDigit);
                    if (digits is < 8 or > 16)
                        continue;
                    AddUnique(evidence, "public-phone", phone, url, "Telephone number explicitly published on the company website.", observed);
                }

                if (evidence.Count >= 10)
                    break;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException)
            {
                // A blocked or unavailable public page is not evidence and must not
                // prevent enrichment of other prospects in the same job.
            }
            catch (TaskCanceledException)
            {
                // Per-request timeout: continue with the next public contact page.
            }
        }

        return new(Name, evidence, evidence.Count == 0
            ? "No explicit public email or telephone contact details were found on the checked pages."
            : null);
    }

    private static void AddUnique(
        ICollection<ProspectEnrichmentEvidence> evidence,
        string field,
        string value,
        Uri source,
        string description,
        DateTime observedAtUtc)
    {
        if (evidence.Any(x => x.Field == field && x.Value.Equals(value, StringComparison.OrdinalIgnoreCase)))
            return;
        evidence.Add(new(field, value, source.AbsoluteUri, description, observedAtUtc));
    }
}
