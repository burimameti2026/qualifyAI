using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using LeadsAI.Domain.Core.Portal;

namespace LeadsAI.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/public/portal")]
public sealed class RenovaPublicPortalController(ITenantContext tenant, AppDbContext db) : ControllerBase
{
    private bool IsTenant(Guid tenantId) => tenant.Current?.Id == tenantId;

    [HttpGet("{tenantId:guid}/site-content")]
    public async Task<IActionResult> SiteContent(Guid tenantId, CancellationToken ct = default)
    {
        if (!IsTenant(tenantId)) return NotFound();
        var setting = await db.TenantSettings.AsNoTracking().SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Key == "renova.portal.content.v1", ct);
        if (setting is null || string.IsNullOrWhiteSpace(setting.Value)) return NotFound();
        var content = JsonSerializer.Deserialize<RenovaSiteContentDocument>(setting.Value);
        if (content is null || !string.Equals(content.Status, "Published", StringComparison.OrdinalIgnoreCase)) return NotFound();
        return Ok(content);
    }

    [HttpGet("{tenantId:guid}/products")]
    public async Task<IActionResult> Products(Guid tenantId, [FromQuery] string language = "en", CancellationToken ct = default)
    {
        if (!IsTenant(tenantId)) return NotFound();
        language = NormalizeLanguage(language); var publications = await db.PortalPublications.Where(x => x.TenantId == tenantId && x.Status == "Published" && x.IsVisible).OrderBy(x => x.Slug).ToListAsync(ct); var ids = publications.Select(x => x.CatalogProductId).ToList(); if (ids.Count == 0) return Ok(Array.Empty<object>());
        var products = await db.CatalogProducts.Where(x => ids.Contains(x.Id) && x.IsActive && x.TenantId == tenantId).ToListAsync(ct); var localized = await db.ProductLocalizations.Where(x => ids.Contains(x.CatalogProductId) && x.TenantId == tenantId && x.Language == language).ToListAsync(ct);
        return Ok(products.Select(p => { var l = localized.SingleOrDefault(x => x.CatalogProductId == p.Id); var pub = publications.Single(x => x.CatalogProductId == p.Id); return new { p.Id, slug = pub.Slug, name = l?.Name ?? p.Name, shortDescription = l?.ShortDescription ?? p.ShortDescription, description = l?.Description ?? p.Description, keyBenefits = l?.KeyBenefits ?? p.KeyBenefits, applications = l?.Applications ?? p.Applications, version = pub.Version }; }));
    }

    [HttpGet("{tenantId:guid}/products/{slug}")]
    public async Task<IActionResult> Product(Guid tenantId, string slug, [FromQuery] string language = "en", CancellationToken ct = default)
    {
        if (!IsTenant(tenantId)) return NotFound();
        language = NormalizeLanguage(language); var publication = await db.PortalPublications.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Slug == slug && x.Status == "Published" && x.IsVisible, ct); if (publication is null) return NotFound();
        var p = await db.CatalogProducts.SingleAsync(x => x.Id == publication.CatalogProductId && x.TenantId == tenantId, ct); var l = await db.ProductLocalizations.SingleOrDefaultAsync(x => x.CatalogProductId == p.Id && x.TenantId == tenantId && x.Language == language, ct); var assets = await db.ProductAssets.Where(x => x.CatalogProductId == p.Id && x.TenantId == tenantId && (x.Language == null || x.Language == language) && x.IsPublic).ToListAsync(ct); var variants = await db.ProductVariants.Where(x => x.CatalogProductId == p.Id && x.TenantId == tenantId && x.IsActive).ToListAsync(ct);
        return Ok(new { p.Id, slug = publication.Slug, name = l?.Name ?? p.Name, shortDescription = l?.ShortDescription ?? p.ShortDescription, description = l?.Description ?? p.Description, keyBenefits = l?.KeyBenefits ?? p.KeyBenefits, applications = l?.Applications ?? p.Applications, p.TechnicalSpecifications, assets, variants, version = publication.Version });
    }

    [HttpPost("{tenantId:guid}/inquiries")]
    public async Task<IActionResult> Inquiry(Guid tenantId, PortalInquiry request, CancellationToken ct)
    {
        if (!IsTenant(tenantId)) return NotFound();
        request.Id = Guid.NewGuid(); request.TenantId = tenantId; request.CreatedAt = DateTimeOffset.UtcNow; request.Status = "New"; if (request.CatalogProductId.HasValue && !await db.CatalogProducts.AnyAsync(x => x.Id == request.CatalogProductId && x.TenantId == tenantId && x.IsActive, ct)) request.CatalogProductId = null; db.PortalInquiries.Add(request); await db.SaveChangesAsync(ct); return Accepted(new { request.Id, request.Status });
    }

    private static string NormalizeLanguage(string value) => value.Trim().ToLowerInvariant() switch { "mk" or "mk-mk" => "mk", "sq" or "sq-al" => "sq", "de" or "de-de" => "de", _ => "en" };
}
