using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.Domain.Core.Catalog;
using QualifyAI.Domain.Core.Portal;
using QualifyAI.Domain.Core.Promotions;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/renova/catalog")]
public sealed class RenovaCatalogController(ITenantContext tenant, AppDbContext db) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("categories")]
    public Task<List<ProductCategory>> Categories(CancellationToken ct) => db.ProductCategories.Where(x => x.TenantId == TenantId).OrderBy(x => x.SortOrder).ToListAsync(ct);

    [HttpPost("categories")]
    public async Task<IActionResult> CreateCategory(ProductCategory item, CancellationToken ct)
    {
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; db.ProductCategories.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpGet("products")]
    public async Task<IActionResult> Products(CancellationToken ct) => Ok(await db.CatalogProducts.Where(x => x.TenantId == TenantId).OrderBy(x => x.Name).ToListAsync(ct));

    [HttpGet("products/{id:guid}")]
    public async Task<IActionResult> Product(Guid id, CancellationToken ct)
    {
        var product = await db.CatalogProducts.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId, ct);
        if (product is null) return NotFound();
        return Ok(new { product, variants = await db.ProductVariants.Where(x => x.CatalogProductId == id && x.TenantId == TenantId).ToListAsync(ct), assets = await db.ProductAssets.Where(x => x.CatalogProductId == id && x.TenantId == TenantId).ToListAsync(ct), localizations = await db.ProductLocalizations.Where(x => x.CatalogProductId == id && x.TenantId == TenantId).ToListAsync(ct), publication = await db.PortalPublications.SingleOrDefaultAsync(x => x.CatalogProductId == id && x.TenantId == TenantId, ct) });
    }

    [HttpPost("products")]
    public async Task<IActionResult> CreateProduct(CatalogProduct item, CancellationToken ct)
    {
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CreatedAt = DateTimeOffset.UtcNow; db.CatalogProducts.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPut("products/{id:guid}")]
    public async Task<IActionResult> UpdateProduct(Guid id, CatalogProduct request, CancellationToken ct)
    {
        var item = await db.CatalogProducts.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId, ct); if (item is null) return NotFound();
        item.ProductCategoryId = request.ProductCategoryId; item.Name = request.Name; item.Code = request.Code; item.Brand = request.Brand; item.ShortDescription = request.ShortDescription; item.Description = request.Description; item.KeyBenefits = request.KeyBenefits; item.Applications = request.Applications; item.TechnicalSpecifications = request.TechnicalSpecifications; item.IsActive = request.IsActive; item.UpdatedAt = DateTimeOffset.UtcNow;
        var publication = await db.PortalPublications.SingleOrDefaultAsync(x => x.CatalogProductId == id && x.TenantId == TenantId, ct); if (publication?.Status == "Published") { publication.Version++; publication.UpdatedAt = DateTimeOffset.UtcNow; }
        await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPost("products/{id:guid}/variants")]
    public async Task<IActionResult> AddVariant(Guid id, ProductVariant item, CancellationToken ct)
    {
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CatalogProductId = id; db.ProductVariants.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPost("products/{id:guid}/assets")]
    public async Task<IActionResult> AddAsset(Guid id, ProductAsset item, CancellationToken ct)
    {
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CatalogProductId = id; db.ProductAssets.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPut("products/{id:guid}/localizations/{language}")]
    public async Task<IActionResult> UpsertLocalization(Guid id, string language, ProductLocalization request, CancellationToken ct)
    {
        var item = await db.ProductLocalizations.SingleOrDefaultAsync(x => x.TenantId == TenantId && x.CatalogProductId == id && x.Language == language, ct);
        if (item is null) { request.Id = Guid.NewGuid(); request.TenantId = TenantId; request.CatalogProductId = id; request.Language = language; db.ProductLocalizations.Add(request); item = request; }
        else { item.Name = request.Name; item.ShortDescription = request.ShortDescription; item.Description = request.Description; item.KeyBenefits = request.KeyBenefits; item.Applications = request.Applications; }
        await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPost("products/{id:guid}/publish")]
    public async Task<IActionResult> Publish(Guid id, [FromBody] PublishProductRequest request, CancellationToken ct)
    {
        var product = await db.CatalogProducts.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == TenantId, ct); if (product is null) return NotFound();
        var publication = await db.PortalPublications.SingleOrDefaultAsync(x => x.CatalogProductId == id && x.TenantId == TenantId, ct);
        if (publication is null) { publication = new PortalPublication { TenantId = TenantId, CatalogProductId = id, Slug = string.IsNullOrWhiteSpace(request.Slug) ? Slug(product.Name) : Slug(request.Slug), Status = "Published", IsVisible = true, PublishedAt = DateTimeOffset.UtcNow }; db.PortalPublications.Add(publication); }
        else { publication.Status = "Published"; publication.IsVisible = true; publication.Slug = string.IsNullOrWhiteSpace(request.Slug) ? publication.Slug : Slug(request.Slug); publication.Version++; publication.PublishedAt = DateTimeOffset.UtcNow; publication.UpdatedAt = DateTimeOffset.UtcNow; }
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
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CatalogProductId = id; db.TargetMarkets.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    [HttpPost("promotion-plans")]
    public async Task<IActionResult> CreatePlan(ProductPromotionPlan item, CancellationToken ct)
    {
        item.Id = Guid.NewGuid(); item.TenantId = TenantId; item.CreatedAt = DateTimeOffset.UtcNow; db.ProductPromotionPlans.Add(item); await db.SaveChangesAsync(ct); return Ok(item);
    }

    private static string Slug(string value) => string.Join('-', value.Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries));
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
        var publications = await db.PortalPublications.Where(x => x.TenantId == tenantId && x.Status == "Published" && x.IsVisible).OrderBy(x => x.Slug).ToListAsync(ct);
        var ids = publications.Select(x => x.CatalogProductId).ToList(); var products = await db.CatalogProducts.Where(x => ids.Contains(x.Id) && x.IsActive).ToListAsync(ct); var localized = await db.ProductLocalizations.Where(x => ids.Contains(x.CatalogProductId) && x.Language == language).ToListAsync(ct);
        return Ok(products.Select(p => { var l = localized.SingleOrDefault(x => x.CatalogProductId == p.Id); var pub = publications.Single(x => x.CatalogProductId == p.Id); return new { p.Id, slug = pub.Slug, name = l?.Name ?? p.Name, shortDescription = l?.ShortDescription ?? p.ShortDescription, description = l?.Description ?? p.Description, keyBenefits = l?.KeyBenefits ?? p.KeyBenefits, applications = l?.Applications ?? p.Applications, version = pub.Version }; }));
    }

    [HttpGet("{tenantId:guid}/products/{slug}")]
    public async Task<IActionResult> Product(Guid tenantId, string slug, [FromQuery] string language = "en", CancellationToken ct = default)
    {
        var publication = await db.PortalPublications.SingleOrDefaultAsync(x => x.TenantId == tenantId && x.Slug == slug && x.Status == "Published" && x.IsVisible, ct); if (publication is null) return NotFound();
        var p = await db.CatalogProducts.SingleAsync(x => x.Id == publication.CatalogProductId, ct); var l = await db.ProductLocalizations.SingleOrDefaultAsync(x => x.CatalogProductId == p.Id && x.Language == language, ct); var assets = await db.ProductAssets.Where(x => x.CatalogProductId == p.Id && (x.Language == null || x.Language == language)).ToListAsync(ct); var variants = await db.ProductVariants.Where(x => x.CatalogProductId == p.Id && x.IsActive).ToListAsync(ct);
        return Ok(new { p.Id, slug = publication.Slug, name = l?.Name ?? p.Name, shortDescription = l?.ShortDescription ?? p.ShortDescription, description = l?.Description ?? p.Description, keyBenefits = l?.KeyBenefits ?? p.KeyBenefits, applications = l?.Applications ?? p.Applications, p.TechnicalSpecifications, assets, variants, version = publication.Version });
    }

    [HttpPost("{tenantId:guid}/inquiries")]
    public async Task<IActionResult> Inquiry(Guid tenantId, PortalInquiry request, CancellationToken ct)
    {
        request.Id = Guid.NewGuid(); request.TenantId = tenantId; request.CreatedAt = DateTimeOffset.UtcNow; request.Status = "New"; db.PortalInquiries.Add(request); await db.SaveChangesAsync(ct); return Accepted(new { request.Id, request.Status });
    }
}
