using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Tenancy;
using LeadsAI.Domain.Core.Catalog;
using LeadsAI.Domain.Core.Promotions;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api;

public static class CatalogEndpoints
{
    public static IEndpointRouteBuilder MapCatalog(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/catalog");

        g.MapGet("/categories", async (ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
            !tenant.IsResolved ? Results.Unauthorized() : Results.Ok(await db.ProductCategories.AsNoTracking()
                .Where(x => x.TenantId == tenant.Id).OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ToListAsync(ct)));

        g.MapPost("/categories", async (CategoryRequest request, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "Category name is required." });
            if (request.ParentCategoryId.HasValue && !await db.ProductCategories.AnyAsync(x => x.Id == request.ParentCategoryId && x.TenantId == tenant.Id, ct))
                return Results.BadRequest(new { error = "Parent category was not found in this tenant." });
            if (!string.IsNullOrWhiteSpace(request.Code) && await db.ProductCategories.AnyAsync(x => x.TenantId == tenant.Id && x.Code == request.Code.Trim(), ct))
                return Results.Conflict(new { error = "A category with this code already exists." });

            var entity = new ProductCategory
            {
                TenantId = tenant.Id, Name = request.Name.Trim(), Code = Normalize(request.Code),
                Description = request.Description?.Trim(), ParentCategoryId = request.ParentCategoryId,
                SortOrder = request.SortOrder, IsActive = request.IsActive
            };
            db.ProductCategories.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/catalog/categories/{entity.Id}", entity);
        });

        g.MapPut("/categories/{id:guid}", async (Guid id, CategoryRequest request, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            var entity = await db.ProductCategories.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct);
            if (entity is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "Category name is required." });
            var code = Normalize(request.Code);
            if (!string.IsNullOrWhiteSpace(code) && await db.ProductCategories.AnyAsync(x => x.TenantId == tenant.Id && x.Id != id && x.Code == code, ct))
                return Results.Conflict(new { error = "A category with this code already exists." });
            if (request.ParentCategoryId == id) return Results.BadRequest(new { error = "A category cannot be its own parent." });
            if (request.ParentCategoryId.HasValue && !await db.ProductCategories.AnyAsync(x => x.Id == request.ParentCategoryId && x.TenantId == tenant.Id, ct))
                return Results.BadRequest(new { error = "Parent category was not found in this tenant." });

            entity.Name = request.Name.Trim(); entity.Code = code; entity.Description = request.Description?.Trim();
            entity.ParentCategoryId = request.ParentCategoryId; entity.SortOrder = request.SortOrder; entity.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct);
            return Results.Ok(entity);
        });

        g.MapDelete("/categories/{id:guid}", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            var entity = await db.ProductCategories.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct);
            if (entity is null) return Results.NotFound();
            if (await db.CatalogProducts.AnyAsync(x => x.TenantId == tenant.Id && x.ProductCategoryId == id, ct))
                return Results.Conflict(new { error = "Category cannot be deleted while products use it." });
            if (await db.ProductCategories.AnyAsync(x => x.TenantId == tenant.Id && x.ParentCategoryId == id, ct))
                return Results.Conflict(new { error = "Category cannot be deleted while child categories exist." });
            db.ProductCategories.Remove(entity);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapGet("/products", async (string? search, Guid? categoryId, bool? active, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            var query = db.CatalogProducts.AsNoTracking().Where(x => x.TenantId == tenant.Id);
            if (categoryId.HasValue) query = query.Where(x => x.ProductCategoryId == categoryId.Value);
            if (active.HasValue) query = query.Where(x => x.IsActive == active.Value);
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(x => x.Name.Contains(term) || x.Code.Contains(term) || (x.Brand != null && x.Brand.Contains(term)));
            }
            return Results.Ok(await query.OrderBy(x => x.Name).ToListAsync(ct));
        });

        g.MapGet("/products/{id:guid}", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            var product = await db.CatalogProducts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct);
            if (product is null) return Results.NotFound();
            var variants = await db.ProductVariants.AsNoTracking().Where(x => x.TenantId == tenant.Id && x.CatalogProductId == id).ToListAsync(ct);
            var assets = await db.ProductAssets.AsNoTracking().Where(x => x.TenantId == tenant.Id && x.CatalogProductId == id).ToListAsync(ct);
            var localizations = await db.ProductLocalizations.AsNoTracking().Where(x => x.TenantId == tenant.Id && x.CatalogProductId == id).ToListAsync(ct);
            var markets = await db.TargetMarkets.AsNoTracking().Where(x => x.TenantId == tenant.Id && x.CatalogProductId == id).ToListAsync(ct);
            var promotions = await db.ProductPromotionPlans.AsNoTracking().Where(x => x.TenantId == tenant.Id && x.CatalogProductId == id).ToListAsync(ct);
            return Results.Ok(new { product, variants, assets, localizations, targetMarkets = markets, promotions });
        });

        g.MapPost("/products", async (ProductRequest request, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code))
                return Results.BadRequest(new { error = "Product name and code are required." });
            if (!await db.ProductCategories.AnyAsync(x => x.Id == request.ProductCategoryId && x.TenantId == tenant.Id && x.IsActive, ct))
                return Results.BadRequest(new { error = "An active product category in this tenant is required." });
            var code = request.Code.Trim();
            if (await db.CatalogProducts.AnyAsync(x => x.TenantId == tenant.Id && x.Code == code, ct))
                return Results.Conflict(new { error = "A product with this code already exists." });

            var entity = new CatalogProduct
            {
                TenantId = tenant.Id, ProductCategoryId = request.ProductCategoryId, Name = request.Name.Trim(), Code = code,
                Brand = request.Brand?.Trim(), ShortDescription = request.ShortDescription, Description = request.Description,
                KeyBenefits = request.KeyBenefits, Applications = request.Applications,
                TechnicalSpecifications = request.TechnicalSpecifications, IsActive = request.IsActive, CreatedAt = DateTimeOffset.UtcNow
            };
            db.CatalogProducts.Add(entity);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/catalog/products/{entity.Id}", entity);
        });

        g.MapPut("/products/{id:guid}", async (Guid id, ProductRequest request, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            var entity = await db.CatalogProducts.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct);
            if (entity is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Code)) return Results.BadRequest(new { error = "Product name and code are required." });
            if (!await db.ProductCategories.AnyAsync(x => x.Id == request.ProductCategoryId && x.TenantId == tenant.Id && x.IsActive, ct)) return Results.BadRequest(new { error = "An active product category in this tenant is required." });
            var code = request.Code.Trim();
            if (await db.CatalogProducts.AnyAsync(x => x.TenantId == tenant.Id && x.Id != id && x.Code == code, ct)) return Results.Conflict(new { error = "A product with this code already exists." });

            entity.ProductCategoryId = request.ProductCategoryId; entity.Name = request.Name.Trim(); entity.Code = code;
            entity.Brand = request.Brand?.Trim(); entity.ShortDescription = request.ShortDescription; entity.Description = request.Description;
            entity.KeyBenefits = request.KeyBenefits; entity.Applications = request.Applications; entity.TechnicalSpecifications = request.TechnicalSpecifications;
            entity.IsActive = request.IsActive; entity.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.Ok(entity);
        });

        g.MapDelete("/products/{id:guid}", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            var entity = await db.CatalogProducts.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct);
            if (entity is null) return Results.NotFound();
            entity.IsActive = false; entity.UpdatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        g.MapPost("/products/{productId:guid}/variants", async (Guid productId, VariantRequest request, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            if (!await db.CatalogProducts.AnyAsync(x => x.Id == productId && x.TenantId == tenant.Id, ct)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "Variant name is required." });
            if (!string.IsNullOrWhiteSpace(request.Sku) && await db.ProductVariants.AnyAsync(x => x.TenantId == tenant.Id && x.Sku == request.Sku.Trim(), ct)) return Results.Conflict(new { error = "A variant with this SKU already exists." });
            if (request.NetWeight < 0) return Results.BadRequest(new { error = "Net weight cannot be negative." });
            var entity = new ProductVariant { TenantId = tenant.Id, CatalogProductId = productId, Name = request.Name.Trim(), Sku = Normalize(request.Sku), Packaging = request.Packaging, NetWeight = request.NetWeight, WeightUnit = request.WeightUnit, Color = request.Color, Specifications = request.Specifications, IsActive = request.IsActive };
            db.ProductVariants.Add(entity); await db.SaveChangesAsync(ct);
            return Results.Created($"/api/catalog/products/{productId}/variants/{entity.Id}", entity);
        });

        g.MapPut("/products/{productId:guid}/variants/{id:guid}", async (Guid productId, Guid id, VariantRequest request, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            var entity = await db.ProductVariants.SingleOrDefaultAsync(x => x.Id == id && x.CatalogProductId == productId && x.TenantId == tenant.Id, ct);
            if (entity is null) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "Variant name is required." });
            var sku = Normalize(request.Sku);
            if (!string.IsNullOrWhiteSpace(sku) && await db.ProductVariants.AnyAsync(x => x.TenantId == tenant.Id && x.Id != id && x.Sku == sku, ct)) return Results.Conflict(new { error = "A variant with this SKU already exists." });
            entity.Name = request.Name.Trim(); entity.Sku = sku; entity.Packaging = request.Packaging; entity.NetWeight = request.NetWeight; entity.WeightUnit = request.WeightUnit; entity.Color = request.Color; entity.Specifications = request.Specifications; entity.IsActive = request.IsActive;
            await db.SaveChangesAsync(ct); return Results.Ok(entity);
        });

        g.MapPost("/products/{productId:guid}/localizations", async (Guid productId, LocalizationRequest request, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            if (!await db.CatalogProducts.AnyAsync(x => x.Id == productId && x.TenantId == tenant.Id, ct)) return Results.NotFound();
            var language = request.Language.Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(language) || string.IsNullOrWhiteSpace(request.Name)) return Results.BadRequest(new { error = "Language and name are required." });
            if (await db.ProductLocalizations.AnyAsync(x => x.TenantId == tenant.Id && x.CatalogProductId == productId && x.Language == language, ct)) return Results.Conflict(new { error = "A localization for this language already exists." });
            var entity = new ProductLocalization { TenantId = tenant.Id, CatalogProductId = productId, Language = language, Name = request.Name.Trim(), ShortDescription = request.ShortDescription, Description = request.Description, KeyBenefits = request.KeyBenefits, Applications = request.Applications };
            db.ProductLocalizations.Add(entity); await db.SaveChangesAsync(ct); return Results.Created($"/api/catalog/products/{productId}/localizations/{entity.Id}", entity);
        });

        g.MapPost("/products/{productId:guid}/assets", async (Guid productId, AssetRequest request, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            if (!await db.CatalogProducts.AnyAsync(x => x.Id == productId && x.TenantId == tenant.Id, ct)) return Results.NotFound();
            if (string.IsNullOrWhiteSpace(request.AssetType) || string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.Uri)) return Results.BadRequest(new { error = "Asset type, file name and URI are required." });
            var entity = new ProductAsset { TenantId = tenant.Id, CatalogProductId = productId, AssetType = request.AssetType.Trim(), FileName = request.FileName.Trim(), Uri = request.Uri.Trim(), Language = Normalize(request.Language), Title = request.Title?.Trim(), IsPublic = request.IsPublic, CreatedAt = DateTimeOffset.UtcNow };
            db.ProductAssets.Add(entity); await db.SaveChangesAsync(ct); return Results.Created($"/api/catalog/products/{productId}/assets/{entity.Id}", entity);
        });

        g.MapGet("/promotions", async (string? status, Guid? productId, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            var query = db.ProductPromotionPlans.AsNoTracking().Where(x => x.TenantId == tenant.Id);
            if (productId.HasValue) query = query.Where(x => x.CatalogProductId == productId.Value);
            if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status.Trim());
            return Results.Ok(await query.OrderByDescending(x => x.CreatedAt).ToListAsync(ct));
        });

        g.MapPost("/promotions", async (PromotionRequest request, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.CampaignLanguage)) return Results.BadRequest(new { error = "Promotion name and campaign language are required." });
            if (!await db.CatalogProducts.AnyAsync(x => x.Id == request.CatalogProductId && x.TenantId == tenant.Id, ct)) return Results.BadRequest(new { error = "Product was not found in this tenant." });
            if (!await db.TargetMarkets.AnyAsync(x => x.Id == request.TargetMarketId && x.TenantId == tenant.Id && x.CatalogProductId == request.CatalogProductId, ct)) return Results.BadRequest(new { error = "Target market was not found for this product." });
            var entity = new ProductPromotionPlan { TenantId = tenant.Id, CatalogProductId = request.CatalogProductId, TargetMarketId = request.TargetMarketId, Name = request.Name.Trim(), CampaignLanguage = request.CampaignLanguage.Trim().ToLowerInvariant(), Status = string.IsNullOrWhiteSpace(request.Status) ? "Draft" : request.Status.Trim(), TargetCustomerProfile = request.TargetCustomerProfile, QualificationRules = request.QualificationRules, MessagingStrategy = request.MessagingStrategy, EnableAutonomousProspecting = request.EnableAutonomousProspecting, EnableAutomaticCampaignEnrollment = request.EnableAutomaticCampaignEnrollment, CreatedAt = DateTimeOffset.UtcNow };
            db.ProductPromotionPlans.Add(entity); await db.SaveChangesAsync(ct); return Results.Created($"/api/catalog/promotions/{entity.Id}", entity);
        });

        g.MapPost("/promotions/{id:guid}/activate", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            var entity = await db.ProductPromotionPlans.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct);
            if (entity is null) return Results.NotFound();
            if (!await db.CatalogProducts.AnyAsync(x => x.Id == entity.CatalogProductId && x.TenantId == tenant.Id && x.IsActive, ct)) return Results.Conflict(new { error = "The product must be active before its promotion can be activated." });
            if (!await db.TargetMarkets.AnyAsync(x => x.Id == entity.TargetMarketId && x.TenantId == tenant.Id && x.IsActive, ct)) return Results.Conflict(new { error = "The target market must be active before its promotion can be activated." });
            entity.Status = "Active"; entity.ActivatedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct); return Results.Ok(entity);
        });

        return app;
    }

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public sealed record CategoryRequest(string Name, string? Code, string? Description, int SortOrder = 0, Guid? ParentCategoryId = null, bool IsActive = true);
    public sealed record ProductRequest(Guid ProductCategoryId, string Name, string Code, string? Brand, string? ShortDescription, string? Description, string? KeyBenefits, string? Applications, string? TechnicalSpecifications, bool IsActive = true);
    public sealed record VariantRequest(string Name, string? Sku, string? Packaging, decimal? NetWeight, string? WeightUnit, string? Color, string? Specifications, bool IsActive = true);
    public sealed record LocalizationRequest(string Language, string Name, string? ShortDescription, string? Description, string? KeyBenefits, string? Applications);
    public sealed record AssetRequest(string AssetType, string FileName, string Uri, string? Language, string? Title, bool IsPublic = false);
    public sealed record PromotionRequest(Guid CatalogProductId, Guid TargetMarketId, string Name, string CampaignLanguage, string? Status, string? TargetCustomerProfile, string? QualificationRules, string? MessagingStrategy, bool EnableAutonomousProspecting = false, bool EnableAutomaticCampaignEnrollment = false);
}
