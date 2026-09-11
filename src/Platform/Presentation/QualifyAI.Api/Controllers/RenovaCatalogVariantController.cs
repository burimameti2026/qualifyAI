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
    private Guid TenantId => tenant.TenantId();

    [HttpPut("products/{productId:guid}/variants/{variantId:guid}")]
    public async Task<IActionResult> UpdateVariant(Guid productId, Guid variantId, ProductVariant request, CancellationToken ct)
    {
        var productExists = await db.CatalogProducts.AnyAsync(
            x => x.Id == productId && x.TenantId == TenantId && x.IsActive, ct);
        if (!productExists) return NotFound();

        var item = await db.ProductVariants.SingleOrDefaultAsync(
            x => x.Id == variantId && x.CatalogProductId == productId && x.TenantId == TenantId && x.IsActive, ct);
        if (item is null) return NotFound();

        item.Name = string.IsNullOrWhiteSpace(request.Name) ? "Default" : request.Name.Trim();
        item.Sku = request.Sku?.Trim();
        item.Packaging = request.Packaging?.Trim();
        item.NetWeight = request.NetWeight;
        item.WeightUnit = request.WeightUnit?.Trim();
        item.Color = request.Color?.Trim();
        item.Specifications = request.Specifications?.Trim();

        await db.SaveChangesAsync(ct);
        return Ok(item);
    }
}
