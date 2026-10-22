using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Access;
using LeadsAI.BuildingBlocks.Security.Authorization;
using LeadsAI.Domain;
using LeadsAI.Domain.Core;
using LeadsAI.Api.Services;
using LeadsAI.Api.Importing;

namespace LeadsAI.Api.Controllers;

[ApiController]
[Authorize]
[RequireModule(QualifyAiModules.Crm)]
[Route("api/acquisition")]
public sealed class ProspectsController(AppDbContext db, ITenantContext tenant) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("prospects")]
    [RequirePermission(QualifyAiPermissions.CrmRead)]
    public async Task<IActionResult> Prospects([FromQuery] int minimumScore = 0, CancellationToken ct = default)
    {
        var threshold = Math.Clamp(minimumScore, 0, 100) * 100;
        var rows = await db.Prospects.AsNoTracking()
            .Where(x => x.TenantId == TenantId && x.FitScore * 55 + x.IntentScore * 45 >= threshold)
            .OrderByDescending(x => x.FitScore * 55 + x.IntentScore * 45)
            .ToListAsync(ct);
        return Ok(rows);
    }

    [HttpPost("prospects")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddProspect([FromBody] Prospect input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.CompanyName) || string.IsNullOrWhiteSpace(input.Domain))
            return BadRequest(new { detail = "Company name and domain are required." });
        var tenantId = TenantId;
        var domain = NormalizeDomain(input.Domain);
        var existing = await db.Prospects.FirstOrDefaultAsync(
            x => x.TenantId == tenantId && x.Domain.ToLower() == domain, ct);
        if (existing is not null)
            return Conflict(new { detail = "A prospect with this domain already exists in the workspace.", prospectId = existing.Id });

        input.Id = Guid.NewGuid();
        input.TenantId = tenantId;
        input.Domain = domain;
        input.CompanyName = input.CompanyName.Trim();
        input.Email = input.Email?.Trim() ?? string.Empty;
        input.CreatedAtUtc = input.UpdatedAtUtc = DateTime.UtcNow;
        input.Evaluate(input.FitScore, input.IntentScore);
        db.Prospects.Add(input);
        await db.SaveChangesAsync(ct);
        return Created($"/api/acquisition/prospects/{input.Id}", input);
    }

    [HttpPost("prospects/{id:guid}/signals")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    public async Task<IActionResult> AddSignal(Guid id, [FromBody] ProspectSignal input, CancellationToken ct)
    {
        var tenantId = TenantId;
        var prospect = await db.Prospects.FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Id == id, ct);
        if (prospect is null) return NotFound();
        if (string.IsNullOrWhiteSpace(input.Evidence))
            return BadRequest(new { detail = "Evidence is required to add an intent signal." });

        input.Id = Guid.NewGuid();
        input.TenantId = tenantId;
        input.ProspectId = id;
        input.Type = input.Type?.Trim() ?? string.Empty;
        input.Source = input.Source?.Trim() ?? string.Empty;
        input.Evidence = input.Evidence.Trim();
        input.Score = Math.Clamp(input.Score, 0, 100);
        input.SourceUrl = input.SourceUrl?.Trim() ?? string.Empty;
        input.ObservedAtUtc = DateTime.UtcNow;
        db.ProspectSignals.Add(input);
        prospect.Evaluate(prospect.FitScore, Math.Clamp(prospect.IntentScore + input.Score, 0, 100));
        await db.SaveChangesAsync(ct);
        return Ok(prospect);
    }

    [HttpPost("prospects/import/preview")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> PreviewImport([FromForm] IFormFile? file, [FromForm] string? sheetName, [FromForm] int? headerRow, CancellationToken ct)
    {
        if (file is null) return BadRequest(new { detail = "Choose a CSV or XLSX file." });
        try { return Ok(await ProspectDatasetReader.ReadAsync(file, sheetName, headerRow, ct)); }
        catch (InvalidOperationException ex) { return BadRequest(new { detail = ex.Message }); }
        catch (InvalidDataException) { return BadRequest(new { detail = "The XLSX file is damaged or cannot be read." }); }
    }

    [HttpPost("prospects/import")]
    [RequirePermission(QualifyAiPermissions.CrmManage)]
    [RequestSizeLimit(15_000_000)]
    public async Task<IActionResult> ImportProspects([FromBody] ProspectImportRequest input, CancellationToken ct)
    {
        if (input.Prospects is null || input.Prospects.Length is < 1 or > 10_000)
            return BadRequest(new { detail = "Import between 1 and 10,000 companies per batch." });
        if (string.IsNullOrWhiteSpace(input.Source) || !input.ComplianceConfirmed)
            return BadRequest(new { detail = "Record the licensed/public source and confirm that this company data may be processed." });

        var tenantId = TenantId;
        if (input.IcpProfileId.HasValue &&
            !await db.IcpProfiles.AnyAsync(x => x.TenantId == tenantId && x.Id == input.IcpProfileId.Value, ct))
            return BadRequest(new { detail = "The selected ICP does not belong to this tenant." });

        var now = DateTime.UtcNow;
        var existing = await db.Prospects.Where(x => x.TenantId == tenantId).ToListAsync(ct);
        var byDomain = existing.Where(x => !string.IsNullOrWhiteSpace(x.Domain))
            .GroupBy(x => NormalizeDomain(x.Domain), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);
        var included = new List<Prospect>();
        var added = new List<Prospect>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var rejected = 0;
        var duplicates = 0;
        var updated = 0;

        foreach (var row in input.Prospects)
        {
            var domain = NormalizeDomain(row.Domain ?? string.Empty);
            if (string.IsNullOrWhiteSpace(row.CompanyName) || string.IsNullOrWhiteSpace(domain))
            {
                rejected++;
                continue;
            }
            if (!seen.Add(domain))
            {
                duplicates++;
                continue;
            }

            if (byDomain.TryGetValue(domain, out var prospect))
            {
                prospect.ContactName = FirstNonEmpty(row.ContactName, prospect.ContactName);
                prospect.Email = FirstNonEmpty(row.Email, prospect.Email);
                prospect.JobTitle = FirstNonEmpty(row.JobTitle, prospect.JobTitle);
                prospect.Industry = FirstNonEmpty(row.Industry, prospect.Industry);
                prospect.Country = FirstNonEmpty(row.Country, prospect.Country);
                prospect.Source = FirstNonEmpty(row.Source, input.Source);
                prospect.SourceUrl = FirstNonEmpty(row.SourceUrl, prospect.SourceUrl);
                prospect.DatasetOrigin = FirstNonEmpty(row.DatasetOrigin, prospect.DatasetOrigin);
                prospect.Evaluate(row.FitScore, row.IntentScore);
                prospect.UpdatedAtUtc = now;
                included.Add(prospect);
                updated++;
                continue;
            }

            prospect = new Prospect
            {
                TenantId = tenantId,
                CompanyName = row.CompanyName.Trim(),
                Domain = domain,
                ContactName = row.ContactName?.Trim() ?? string.Empty,
                Email = row.Email?.Trim() ?? string.Empty,
                JobTitle = row.JobTitle?.Trim() ?? string.Empty,
                Industry = row.Industry?.Trim() ?? string.Empty,
                Country = row.Country?.Trim() ?? string.Empty,
                Source = string.IsNullOrWhiteSpace(row.Source) ? input.Source.Trim() : row.Source.Trim(),
                Priority = row.Priority?.Trim() ?? string.Empty,
                ContactReadiness = row.ContactReadiness?.Trim() ?? string.Empty,
                SuggestedBuyer = row.SuggestedBuyer?.Trim() ?? string.Empty,
                SizeBand = row.SizeBand?.Trim() ?? string.Empty,
                PainHypothesis = row.PainHypothesis?.Trim() ?? string.Empty,
                Offer = row.Offer?.Trim() ?? string.Empty,
                SourceUrl = row.SourceUrl?.Trim() ?? string.Empty,
                VerificationStatus = row.VerificationStatus?.Trim() ?? string.Empty,
                OutreachStatus = row.OutreachStatus?.Trim() ?? string.Empty,
                DatasetOrigin = row.DatasetOrigin?.Trim() ?? string.Empty,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            };
            prospect.Evaluate(row.FitScore, row.IntentScore);
            added.Add(prospect);
            included.Add(prospect);
            byDomain[domain] = prospect;
        }

        db.Prospects.AddRange(added);
        Guid? targetListId = null;
        if (!string.IsNullOrWhiteSpace(input.TargetListName) && included.Count > 0)
        {
            var list = new TargetList
            {
                TenantId = tenantId,
                CampaignId = Guid.Empty,
                Name = input.TargetListName.Trim(),
                Description = $"Imported from {input.Source.Trim()} on {now:yyyy-MM-dd}. {included.Count} unique companies.",
                IcpProfileId = input.IcpProfileId,
                Dynamic = false
            };
            db.TargetLists.Add(list);
            db.TargetListMembers.AddRange(included.Select(prospect => new TargetListMember
            {
                TenantId = tenantId,
                TargetListId = list.Id,
                ProspectId = prospect.Id,
                AddedAtUtc = now
            }));
            targetListId = list.Id;
        }

        await db.SaveChangesAsync(ct);
        return Ok(new
        {
            received = input.Prospects.Length,
            imported = added.Count,
            updated,
            duplicates,
            rejected,
            targetListId,
            nextStep = targetListId.HasValue ? "create-campaign" : "create-target-list"
        });
    }

    private static string NormalizeDomain(string value)
    {
        var domain = value.Trim().ToLowerInvariant();
        if (domain.StartsWith("https://")) domain = domain[8..];
        else if (domain.StartsWith("http://")) domain = domain[7..];
        if (domain.StartsWith("www.")) domain = domain[4..];
        return domain.TrimEnd('/').Split('/')[0];
    }

    private static string FirstNonEmpty(string? candidate, string fallback)
        => string.IsNullOrWhiteSpace(candidate) ? fallback : candidate.Trim();
}

public sealed record ProspectImportRequest(
    string Source,
    bool ComplianceConfirmed,
    string? TargetListName,
    Guid? IcpProfileId,
    ProspectImportRow[] Prospects);

public sealed record ProspectImportRow(
    string? CompanyName, string? Domain, string? ContactName, string? Email, string? JobTitle,
    string? Industry, string? Country, string? Source, string? Priority, string? ContactReadiness,
    string? SuggestedBuyer, string? SizeBand, string? PainHypothesis, string? Offer, string? SourceUrl,
    string? VerificationStatus, string? OutreachStatus, string? DatasetOrigin, int FitScore, int IntentScore);
