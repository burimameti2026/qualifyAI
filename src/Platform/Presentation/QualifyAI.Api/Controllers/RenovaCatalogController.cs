using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.Domain.Core.Catalog;
using QualifyAI.Domain.Core.Portal;
using QualifyAI.Domain.Core.Promotions;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/renova/catalog")]
public sealed class RenovaCatalogController(ITenantContext tenant, AppDbContext db) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct)
    {
        var tenantId = TenantId;
        var categories = await db.ProductCategories.Where(x => x.TenantId == tenantId && x.IsActive).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct);
        if (categories.Count == 0)
        {
            categories = [
                new ProductCategory { TenantId = tenantId, Name = "Plasters / Putz", Code = "PUTZ", SortOrder = 10 },
                new ProductCategory { TenantId = tenantId, Name = "Adhesives", Code = "ADH", SortOrder = 20 },
                new ProductCategory { TenantId = tenantId, Name = "Primers", Code = "PRIMER", SortOrder = 30 },
                new ProductCategory { TenantId = tenantId, Name = "Exterior Solutions", Code = "EXT", SortOrder = 40 }
            ];
            db.ProductCategories.AddRange(categories);
            await db.SaveChangesAsync(ct);
        }
        return Ok(categories);
    }

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(ProductCategory item, CancellationToken ct)
    {
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; db.ProductCategories.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpGet("products")]
    public async Task<IActionResult> Products(CancellationToken ct) => Ok(await db.CatalogProducts.Where(x => x.TenantId == TenantId && x.IsActive).OrderBy(x => x.Name).Select(x => new
    {
        x.Id, x.Name, x.Code, x.Brand, x.ShortDescription, x.ProductCategoryId,
        variants = db.ProductVariants.Count(v => v.TenantId == TenantId && v.CatalogProductId == x.Id && v.IsActive),
        languages = db.ProductLocalizations.Where(l => l.TenantId == TenantId && l.CatalogProductId == x.Id).Select(l => l.Language).Distinct().OrderBy(l => l).ToList(),
        publication = db.PortalPublications.Where(p => p.TenantId == TenantId && p.CatalogProductId == x.Id).Select(p => new { p.Status, p.IsVisible, p.Version, p.Slug }).FirstOrDefault()
    }).ToListAsync(ct));

    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> Product(Guid id, CancellationToken ct)
    {
        var product = await db.CatalogProducts.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && x.IsActive, ct);
        if (product is null) return NotFound();
        return Ok(new { product, variants = await db.ProductVariants.Where(x => x.CatalogProductId == id && x.TenantId == TenantId && x.IsActive).ToListAsync(ct), assets = await db.ProductAssets.Where(x => x.CatalogProductId == id && x.TenantId == TenantId).ToListAsync(ct), localizations = await db.ProductLocalizations.Where(x => x.CatalogProductId == id && x.TenantId == TenantId).ToListAsync(ct), publication = await db.PortalPublications.SingleOrDefaultAsync(x => x.CatalogProductId == id && x.TenantId == TenantId, ct) });
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(CatalogProduct item, CancellationToken ct)
    {
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CreatedAt = DateTimeOffset.UtcNow;
        if (!await db.ProductCategories.AnyAsync(x => x.Id == item.ProductCategoryId && x.TenantId == TenantId && x.IsActive, ct)) return BadRequest(new { code = "category_not_found" });
        if (await db.CatalogProducts.AnyAsync(x => x.TenantId == TenantId && x.Code == item.Code, ct)) return Conflict(new { code = "product_code_exists" });
        db.CatalogProducts.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPut("products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, CatalogProduct request, CancellationToken ct)
    {
        var item = await db.CatalogProducts.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && x.IsActive, ct); if (item is null) return NotFound();
        item.ProductCategoryId = request.ProductCategoryId; item.Name = request.Name.Trim(); item.Code = request.Code.Trim(); item.Brand = request.Brand?.Trim(); item.ShortDescription = request.ShortDescription?.Trim(); item.Description = request.Description?.Trim(); item.KeyBenefits = request.KeyBenefits?.Trim(); item.Applications = request.Applications?.Trim(); item.TechnicalSpecifications = request.TechnicalSpecifications?.Trim(); item.UpdatedAt = DateTimeOffset.UtcNow;
        if (!await db.ProductCategories.AnyAsync(x => x.Id == item.ProductCategoryId && x.TenantId == TenantId && x.IsActive, ct)) return BadRequest(new { code = "category_not_found" });
        await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPost("products/{id:guid}/variants")]
    public async Task<IActionResult> AddVariant(Guid id, ProductVariant item, CancellationToken ct)
    {
        if (!await db.CatalogProducts.AnyAsync(x => x.Id == id && x.TenantId == TenantId && x.IsActive, ct)) return NotFound();
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CatalogProductId = id; db.ProductVariants.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPost("products/{id:guid}/assets")]
    public async Task<IActionResult> AddAsset(Guid id, ProductAsset item, CancellationToken ct)
    {
        if (!await db.CatalogProducts.AnyAsync(x => x.Id == id && x.TenantId == TenantId && x.IsActive, ct)) return NotFound();
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CatalogProductId = id; db.ProductAssets.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPut("products/{id:guid}/localizations/{language}")]
    public async Task<IActionResult> UpsertLocalization(Guid id, string language, ProductLocalization request, CancellationToken ct)
    {
        language = NormalizeLanguage(language);
        var product = await db.CatalogProducts.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && x.IsActive, ct); if (product is null) return NotFound();
        var item = await db.ProductLocalizations.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.CatalogProductId == id && x.Language == language, ct);
        if (item is null) { request.Id = Guid.NewGuid(); request.TenantId = TenantId; request.CatalogProductId = id; request.Language = language; db.ProductLocalizations.Add(request); item = request; }
        else { item.Name = request.Name; item.ShortDescription = request.ShortDescription; item.Description = request.Description; item.KeyBenefits = request.KeyBenefits; item.Applications = request.Applications; }
        await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPost("products/{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishProductRequest request, CancellationToken ct)
    {
        var product = await db.CatalogProducts.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId && x.IsActive, ct); if (product is null) return NotFound();
        var publication = await db.PortalPublications.SingleOrDefaultAsync(x => x.CatalogProductId == id && x.TenantId == TenantId, ct);
        if (publication is null) { publication = new PortalPublication { TenantId = TenantId, CatalogProductId = id, Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug(product.Name, id) : Slug(request.Slug, id), Status = "Published", IsVisible = true, PublishedAt = DateTimeOffset.UtcNow }; db.PortalPublications.Add(publication); }
        else { publication.Status = "Published"; publication.IsVisible = true; publication.Slug = string.IsNullOrWhiteSpace(request.Slug) ? publication.Slug : Slug(request.Slug, id); publication.Version++; publication.PublishedAt = DateTimeOffset.UtcNow; publication.UpdatedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(ct); return Ok(publication);
    }

    [HttpPost("products/{id:guid}/unpublish")]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        var publication = await db.PortalPublications.SingleOrDefaultAsync(x => x.CatalogProductId == id && x.TenantId == TenantId, ct); if (publication is null) return NotFound(); publication.Status = "Draft"; publication.IsVisible = false; publication.UpdatedAt = DateTimeOffset.UtcNow; await db.SaveChangesAsync(ct); return Ok(publication);
    }

    [HttpPost("products/{id:guid}/markets")]
    public async Task<IActionResult> AddMarket(Guid id, TargetMarket item, CancellationToken ct)
    {
        if (!await db.CatalogProducts.AnyAsync(x => x.Id == id && x.TenantId == TenantId && x.IsActive, ct)) return NotFound();
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CatalogProductId = id; item.CountryCode = item.CountryCode.Trim().ToUpperInvariant(); item.DefaultLanguage = NormalizeLanguage(item.DefaultLanguage); db.TargetMarkets.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPost("promotion-plans")]
    public async Task<IActionResult> CreatePlan(ProductPromotionPlan item, CancellationToken ct)
    {
        if (!await db.CatalogProducts.AnyAsync(x => x.Id == item.CatalogProductId && x.TenantId == TenantId && x.IsActive, ct)) return BadRequest(new { code = "product_not_found" });
        if (!await db.TargetMarkets.AnyAsync(x => x.Id == item.TargetMarketId && x.TenantId == TenantId && x.CatalogProductId == item.CatalogProductId && x.IsActive, ct)) return BadRequest(new { code = "target_market_not_found" });
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CreatedAt = DateTimeOffset.UtcNow; item.CampaignLanguage = NormalizeLanguage(item.CampaignLanguage); db.ProductPromotionPlans.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    private static string NormalizeLanguage(string value) => value.Trim().ToLowerInvariant() switch { "mk" or "mk-mk" => "mk", "sq" or "sq-al" => "sq", "de" or "de-de" => "de", _ => "en" };
    private static string Slug(string value, Guid id) { var slug = string.Join('-', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries)); return string.IsNullOrWhiteSpace(slug) ? id.ToString("N") : $"{slug}-{id.ToString("N")[..8]}"; }
}

public sealed record PublishProductRequest(string? Slug);

[ApiController]
[AllowAnonymous]
[Route("api/public/portal")]
public sealed class RenovaPublicPortalController(AppDbContext db) : ControllerBase
{
    [HttpGet("{tenantId:guid}/products")]
    public async Task<IActionResult> Products(Guid tenantId, [FromQuery] string language = "en", CancellationToken ct = default)
    {
        language = NormalizeLanguage(language);
        var publications = await db.PortalPublications.Where(x => x.TenantId == tenantId && x.Status == "Published" && x.IsVisible).OrderBy(x => x.Slug).ToListAsync(ct);
        var ids = publications.Select(x => x.CatalogProductId).ToList(); var products = await db.CatalogProducts.Where(x => ids.Contains(x.Id) && x.IsActive && x.TenantId == tenantId).ToListAsync(ct); var localized = await db.ProductLocalizations.Where(x => ids.Contains(x.CatalogProductId) && x.TenantId == tenantId && x.Language == language).ToListAsync(ct);
        return Ok(products.Select(p => { var l = localized.SingleOrDefault(x => x.CatalogProductId == p.Id); var pub = publications.Single(x => x.CatalogProductId == p.Id); return new { p.Id, slug = pub.Slug, name = l?.Name ?? p.Name, shortDescription = l?.ShortDescription ?? p.ShortDescription, description = l?.Description ?? p.Description, keyBenefits = l?.KeyBenefits ?? p.KeyBenefits, applications = l?.Applications ?? p.Applications, version = pub.Version }; }));
    }

    [HttpGet("{tenantId:guid}/products/{slug}")]
    public async Task<IActionResult> Product(Guid tenantId, string slug, [FromQuery] string language = "en", CancellationToken ct = default)
    {
        language = NormalizeLanguage(language); var publication = await db.PortalPublications.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Slug == slug && x.Status == "Published" && x.IsVisible, ct); if (publication is null) return NotFound();
        var p = await db.CatalogProducts.SingleAsync(x => x.Id == publication.CatalogProductId && x.TenantId == tenantId, ct); var l = await db.ProductLocalizations.SingleOrDefaultAsync(x => x.CatalogProductId == p.Id && x.TenantId == tenantId && x.Language == language, ct); var assets = await db.ProductAssets.Where(x => x.CatalogProductId == p.Id && x.TenantId == tenantId && (x.Language == null || x.Language == language) && x.IsPublic).ToListAsync(ct); var variants = await db.ProductVariants.Where(x => x.CatalogProductId == p.Id && x.TenantId == tenantId && x.IsActive).ToListAsync(ct);
        return Ok(new { p.Id, slug = publication.Slug, name = l?.Name ?? p.Name, shortDescription = l?.ShortDescription ?? p.ShortDescription, description = l?.Description ?? p.Description, keyBenefits = l?.KeyBenefits ?? p.KeyBenefits, applications = l?.Applications ?? p.Applications, p.TechnicalSpecifications, assets, variants, version = publication.Version });
    }

    [HttpPost("{tenantId:guid}/inquiries")]
    public async Task<IActionResult> Inquiry(Guid tenantId, PortalInquiry request, CancellationToken ct)
    {
        request.Id = Guid.NewGuid(); request.TenantId = tenantId; request.CreatedAt = DateTimeOffset.UtcNow; request.Status = "New"; db.PortalInquiries.Add(request); await db.SaveChangesAsync(ct); return Accepted(new { request.Id, request.Status });
    }

    private static string NormalizeLanguage(string value) => value.Trim().ToLowerInvariant() switch { "mk" or "mk-mk" => "mk", "sq" or "sq-al" => "sq", "de" or "de-de" => "de", _ => "en" };
}
