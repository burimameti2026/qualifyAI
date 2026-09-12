using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.Domain.Core.Catalog;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/renova/catalog")]
public sealed class RenovaCatalogVariantController(ITenantContext tenant, AppDbContext db) : ControllerBase
{
    [HttpPut("products/{productId:guid}/variants/{variantId:guid}")]
    public async Task<IActionResult> UpdateVariant(Guid productId, Guid variantId, ProductVariant request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId();
        var productExists = await db.CatalogProducts.AnyAsync(x => x.Id == productId && x.TenantId == tenantId && x.IsActive, ct);
        if (!productExists) return NotFound(new { code = "product_not_found" });

        var variant = await db.ProductVariants.SingleOrDefaultAsync(
            x => x.Id == variantId && x.CatalogProductId == productId && x.TenantId == tenantId && x.IsActive, ct);
        if (variant is null) return NotFound(new { code = "variant_not_found" });

        variant.Name = string.IsNullOrWhiteSpace(request.Name) ? "Default" : request.Name.Trim();
        variant.Sku = request.Sku?.Trim();
        variant.Packaging = request.Packaging?.Trim();
        variant.NetWeight = request.NetWeight;
        variant.WeightUnit = string.IsNullOrWhiteSpace(request.WeightUnit) ? null : request.WeightUnit.Trim();
        variant.Color = request.Color?.Trim();
        variant.Specifications = request.Specifications?.Trim();

        await db.SaveChangesAsync(ct);
        return Ok(variant);
    }
}
