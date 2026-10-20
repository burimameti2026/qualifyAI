using Microsoft.EntityFrameworkCore;
using LeadsAI.BuildingBlocks.Security.Tenancy;
using LeadsAI.Domain;
using LeadsAI.Domain.Core.Catalog;
using LeadsAI.Persistence.SqlServer;

namespace LeadsAI.Api;

public static class EnterpriseOperationsEndpoints
{
    public static IEndpointRouteBuilder MapEnterpriseOperations(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/operations");

        g.MapGet("/facilities", async (ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
            !tenant.IsResolved ? Results.Unauthorized() : Results.Ok(await db.Facilities.AsNoTracking().Where(x => x.TenantId == tenant.Id).OrderBy(x => x.Name).ToListAsync(ct)));
        g.MapPost("/facilities", async (FacilityRequest r, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name)) return Results.BadRequest(new { error = "Facility code and name are required." });
            var code = r.Code.Trim();
            if (await db.Facilities.AnyAsync(x => x.TenantId == tenant.Id && x.Code == code, ct)) return Results.Conflict(new { error = "Facility code already exists." });
            var e = new Facility { TenantId = tenant.Id, Code = code, Name = r.Name.Trim(), Type = r.Type, CountryCode = r.CountryCode?.Trim() ?? "", City = r.City?.Trim() ?? "", Address = r.Address?.Trim() ?? "", ContactName = r.ContactName?.Trim() ?? "", ContactPhone = r.ContactPhone?.Trim() ?? "", ContactEmail = r.ContactEmail?.Trim() ?? "", Capacity = r.Capacity, CapacityUnit = r.CapacityUnit?.Trim() ?? "", Latitude = r.Latitude, Longitude = r.Longitude, IsActive = r.IsActive };
            db.Facilities.Add(e); await db.SaveChangesAsync(ct); return Results.Created($"/api/operations/facilities/{e.Id}", e);
        });
        g.MapPut("/facilities/{id:guid}", async (Guid id, FacilityRequest r, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); var e = await db.Facilities.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct); if (e is null) return Results.NotFound();
            var code = r.Code.Trim(); if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(r.Name)) return Results.BadRequest(new { error = "Facility code and name are required." });
            if (await db.Facilities.AnyAsync(x => x.TenantId == tenant.Id && x.Id != id && x.Code == code, ct)) return Results.Conflict(new { error = "Facility code already exists." });
            e.Code = code; e.Name = r.Name.Trim(); e.Type = r.Type; e.CountryCode = r.CountryCode?.Trim() ?? ""; e.City = r.City?.Trim() ?? ""; e.Address = r.Address?.Trim() ?? ""; e.ContactName = r.ContactName?.Trim() ?? ""; e.ContactPhone = r.ContactPhone?.Trim() ?? ""; e.ContactEmail = r.ContactEmail?.Trim() ?? ""; e.Capacity = r.Capacity; e.CapacityUnit = r.CapacityUnit?.Trim() ?? ""; e.Latitude = r.Latitude; e.Longitude = r.Longitude; e.IsActive = r.IsActive; await db.SaveChangesAsync(ct); return Results.Ok(e);
        });

        g.MapGet("/price-lists", async (ICurrentTenant tenant, AppDbContext db, CancellationToken ct) => !tenant.IsResolved ? Results.Unauthorized() : Results.Ok(await db.PriceLists.AsNoTracking().Where(x => x.TenantId == tenant.Id).OrderBy(x => x.Code).ToListAsync(ct)));
        g.MapPost("/price-lists", async (PriceListRequest r, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); if (string.IsNullOrWhiteSpace(r.Code) || string.IsNullOrWhiteSpace(r.Name)) return Results.BadRequest(new { error = "Price list code and name are required." });
            var code = r.Code.Trim(); if (await db.PriceLists.AnyAsync(x => x.TenantId == tenant.Id && x.Code == code, ct)) return Results.Conflict(new { error = "Price list code already exists." });
            var e = new PriceList { TenantId = tenant.Id, Code = code, Name = r.Name.Trim(), Currency = string.IsNullOrWhiteSpace(r.Currency) ? "EUR" : r.Currency.Trim().ToUpperInvariant(), CustomerType = r.CustomerType?.Trim() ?? "default", CountryCode = r.CountryCode?.Trim() ?? "", ValidFromUtc = r.ValidFromUtc, ValidToUtc = r.ValidToUtc, IsActive = r.IsActive };
            if (e.ValidFromUtc.HasValue && e.ValidToUtc.HasValue && e.ValidFromUtc > e.ValidToUtc) return Results.BadRequest(new { error = "Price list validity range is invalid." });
            db.PriceLists.Add(e); await db.SaveChangesAsync(ct); return Results.Created($"/api/operations/price-lists/{e.Id}", e);
        });
        g.MapPost("/price-lists/{id:guid}/items", async (Guid id, PriceListItemRequest r, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); if (r.UnitPrice < 0 || r.MinimumQuantity < 0 || r.MaximumQuantity < 0) return Results.BadRequest(new { error = "Price and quantities cannot be negative." });
            if (!await db.PriceLists.AnyAsync(x => x.Id == id && x.TenantId == tenant.Id, ct)) return Results.NotFound();
            if (!await db.CatalogProducts.AnyAsync(x => x.Id == r.CatalogProductId && x.TenantId == tenant.Id, ct)) return Results.BadRequest(new { error = "Product was not found in this tenant." });
            if (r.ProductVariantId.HasValue && !await db.ProductVariants.AnyAsync(x => x.Id == r.ProductVariantId && x.TenantId == tenant.Id && x.CatalogProductId == r.CatalogProductId, ct)) return Results.BadRequest(new { error = "Variant was not found for this product." });
            if (r.MinimumQuantity.HasValue && r.MaximumQuantity.HasValue && r.MinimumQuantity > r.MaximumQuantity) return Results.BadRequest(new { error = "Minimum quantity cannot exceed maximum quantity." });
            var e = new PriceListItem { TenantId = tenant.Id, PriceListId = id, CatalogProductId = r.CatalogProductId, ProductVariantId = r.ProductVariantId, Sku = r.Sku?.Trim() ?? "", Description = r.Description?.Trim() ?? "", Unit = string.IsNullOrWhiteSpace(r.Unit) ? "unit" : r.Unit.Trim(), UnitPrice = r.UnitPrice, MinimumQuantity = r.MinimumQuantity, MaximumQuantity = r.MaximumQuantity };
            db.PriceListItems.Add(e); await db.SaveChangesAsync(ct); return Results.Created($"/api/operations/price-lists/{id}/items/{e.Id}", e);
        });

        g.MapGet("/orders", async (OrderStatus? status, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); var q = db.SalesOrders.AsNoTracking().Where(x => x.TenantId == tenant.Id); if (status.HasValue) q = q.Where(x => x.Status == status.Value); return Results.Ok(await q.OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct));
        });
        g.MapGet("/orders/{id:guid}", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); var order = await db.SalesOrders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct); if (order is null) return Results.NotFound(); var items = await db.SalesOrderItems.AsNoTracking().Where(x => x.TenantId == tenant.Id && x.SalesOrderId == id).ToListAsync(ct); return Results.Ok(new { order, items });
        });
        g.MapPost("/orders", async (CreateOrderRequest r, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); if (r.Items is null || r.Items.Count == 0) return Results.BadRequest(new { error = "At least one order item is required." });
            if (r.Items.Any(x => x.Quantity <= 0 || x.UnitPrice < 0 || x.Discount < 0 || x.Tax < 0)) return Results.BadRequest(new { error = "Order quantities must be positive and monetary values cannot be negative." });
            var productIds = r.Items.Select(x => x.CatalogProductId).Distinct().ToList(); var validProducts = await db.CatalogProducts.Where(x => x.TenantId == tenant.Id && productIds.Contains(x.Id) && x.IsActive).Select(x => x.Id).ToListAsync(ct); if (validProducts.Count != productIds.Count) return Results.BadRequest(new { error = "Every order product must be active and belong to this tenant." });
            foreach (var item in r.Items.Where(x => x.ProductVariantId.HasValue)) if (!await db.ProductVariants.AnyAsync(x => x.Id == item.ProductVariantId && x.TenantId == tenant.Id && x.CatalogProductId == item.CatalogProductId && x.IsActive, ct)) return Results.BadRequest(new { error = "An order variant is invalid for its product." });
            var number = string.IsNullOrWhiteSpace(r.Number) ? $"SO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}" : r.Number.Trim(); if (await db.SalesOrders.AnyAsync(x => x.TenantId == tenant.Id && x.Number == number, ct)) return Results.Conflict(new { error = "Order number already exists." });
            var order = new SalesOrder { TenantId = tenant.Id, Number = number, CustomerAccountId = r.CustomerAccountId, CompanyId = r.CompanyId, ContactId = r.ContactId, Channel = r.Channel, Currency = string.IsNullOrWhiteSpace(r.Currency) ? "EUR" : r.Currency.Trim().ToUpperInvariant(), RequestedDeliveryAtUtc = r.RequestedDeliveryAtUtc, Notes = r.Notes?.Trim() ?? "" };
            foreach (var i in r.Items) { var total = i.Quantity * i.UnitPrice - i.Discount + i.Tax; order.Subtotal += i.Quantity * i.UnitPrice; order.DiscountTotal += i.Discount; order.TaxTotal += i.Tax; db.SalesOrderItems.Add(new SalesOrderItem { TenantId = tenant.Id, SalesOrderId = order.Id, CatalogProductId = i.CatalogProductId, ProductVariantId = i.ProductVariantId, Sku = i.Sku?.Trim() ?? "", Description = i.Description?.Trim() ?? "", Quantity = i.Quantity, Unit = string.IsNullOrWhiteSpace(i.Unit) ? "unit" : i.Unit.Trim(), UnitPrice = i.UnitPrice, Discount = i.Discount, Tax = i.Tax, Total = total }); }
            order.GrandTotal = order.Subtotal - order.DiscountTotal + order.TaxTotal; db.SalesOrders.Add(order); await db.SaveChangesAsync(ct); return Results.Created($"/api/operations/orders/{order.Id}", order);
        });
        g.MapPost("/orders/{id:guid}/submit", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) => await ChangeOrderStatus(id, OrderStatus.Submitted, tenant, db, ct));
        g.MapPost("/orders/{id:guid}/confirm", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) => await ChangeOrderStatus(id, OrderStatus.Confirmed, tenant, db, ct));
        g.MapPost("/orders/{id:guid}/cancel", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) => await ChangeOrderStatus(id, OrderStatus.Cancelled, tenant, db, ct));

        g.MapGet("/inventory", async (Guid? facilityId, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); var q = db.StockBalances.AsNoTracking().Where(x => x.TenantId == tenant.Id); if (facilityId.HasValue) q = q.Where(x => x.FacilityId == facilityId.Value); return Results.Ok(await q.OrderBy(x => x.FacilityId).ThenBy(x => x.CatalogProductId).ToListAsync(ct));
        });
        g.MapPost("/inventory/adjust", async (InventoryAdjustmentRequest r, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); if (r.Quantity == 0) return Results.BadRequest(new { error = "Quantity adjustment cannot be zero." });
            if (!await db.Facilities.AnyAsync(x => x.Id == r.FacilityId && x.TenantId == tenant.Id && x.IsActive, ct)) return Results.BadRequest(new { error = "Active facility was not found." });
            if (!await db.CatalogProducts.AnyAsync(x => x.Id == r.CatalogProductId && x.TenantId == tenant.Id && x.IsActive, ct)) return Results.BadRequest(new { error = "Active product was not found." });
            var balance = await db.StockBalances.SingleOrDefaultAsync(x => x.TenantId == tenant.Id && x.FacilityId == r.FacilityId && x.CatalogProductId == r.CatalogProductId && x.ProductVariantId == r.ProductVariantId, ct);
            if (balance is null) { if (r.Quantity < 0) return Results.BadRequest(new { error = "Cannot reduce inventory that has no balance." }); balance = new StockBalance { TenantId = tenant.Id, FacilityId = r.FacilityId, CatalogProductId = r.CatalogProductId, ProductVariantId = r.ProductVariantId, OnHand = 0, Reserved = 0, MinimumLevel = (byte)Math.Clamp(r.MinimumLevel ?? 0m, 0m, byte.MaxValue), Unit = string.IsNullOrWhiteSpace(r.Unit) ? "unit" : r.Unit.Trim() }; db.StockBalances.Add(balance); }
            var next = balance.OnHand + r.Quantity; if (next < balance.Reserved) return Results.Conflict(new { error = "Adjustment would reduce on-hand below reserved stock." }); balance.OnHand = next; if (r.MinimumLevel.HasValue) balance.MinimumLevel = (byte)Math.Clamp(r.MinimumLevel.Value, 0m, byte.MaxValue); if (!string.IsNullOrWhiteSpace(r.Unit)) balance.Unit = r.Unit.Trim();
            var movement = new StockMovement { TenantId = tenant.Id, Number = $"ADJ-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}", CatalogProductId = r.CatalogProductId, ProductVariantId = r.ProductVariantId, Quantity = Math.Abs(r.Quantity), Unit = balance.Unit, FromFacilityId = r.Quantity < 0 ? r.FacilityId : null, ToFacilityId = r.Quantity > 0 ? r.FacilityId : null, Status = StockMovementStatus.Completed, ReceivedAtUtc = DateTime.UtcNow };
            db.StockMovements.Add(movement); await db.SaveChangesAsync(ct); return Results.Ok(new { balance, movement });
        });
        g.MapPost("/inventory/reserve", async (ReserveInventoryRequest r, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); if (r.Quantity <= 0) return Results.BadRequest(new { error = "Reservation quantity must be positive." });
            var b = await db.StockBalances.SingleOrDefaultAsync(x => x.Id == r.StockBalanceId && x.TenantId == tenant.Id, ct); if (b is null) return Results.NotFound(); if (b.Available < r.Quantity) return Results.Conflict(new { error = "Insufficient available stock.", available = b.Available });
            b.Reserved += r.Quantity; var reservation = new StockReservation { TenantId = tenant.Id, StockBalanceId = b.Id, SalesOrderId = r.SalesOrderId, FulfillmentId = r.FulfillmentId, Quantity = r.Quantity, Status = "reserved", ExpiresAtUtc = r.ExpiresAtUtc }; db.StockReservations.Add(reservation); await db.SaveChangesAsync(ct); return Results.Created($"/api/operations/inventory/reservations/{reservation.Id}", reservation);
        });
        g.MapPost("/inventory/reservations/{id:guid}/release", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); var r = await db.StockReservations.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct); if (r is null) return Results.NotFound(); if (r.Status != "reserved") return Results.Conflict(new { error = "Reservation is not active." }); var b = await db.StockBalances.SingleOrDefaultAsync(x => x.Id == r.StockBalanceId && x.TenantId == tenant.Id, ct); if (b is null) return Results.Conflict(new { error = "Stock balance no longer exists." }); b.Reserved = Math.Max(0, b.Reserved - r.Quantity); r.Status = "released"; await db.SaveChangesAsync(ct); return Results.Ok(r);
        });

        g.MapGet("/shipments", async (ShipmentStatus? status, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) => !tenant.IsResolved ? Results.Unauthorized() : Results.Ok(await (status.HasValue ? db.Shipments.AsNoTracking().Where(x => x.TenantId == tenant.Id && x.Status == status.Value) : db.Shipments.AsNoTracking().Where(x => x.TenantId == tenant.Id)).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct)));
        g.MapPost("/shipments", async (CreateShipmentRequest r, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); if (!await db.Fulfillments.AnyAsync(x => x.Id == r.FulfillmentId && x.TenantId == tenant.Id, ct)) return Results.BadRequest(new { error = "Fulfillment was not found." });
            var number = string.IsNullOrWhiteSpace(r.Number) ? $"SHP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}" : r.Number.Trim(); if (await db.Shipments.AnyAsync(x => x.TenantId == tenant.Id && x.Number == number, ct)) return Results.Conflict(new { error = "Shipment number already exists." });
            var e = new Shipment { TenantId = tenant.Id, Number = number, FulfillmentId = r.FulfillmentId, FromFacilityId = r.FromFacilityId, ToFacilityId = r.ToFacilityId, Method = r.Method, CarrierName = r.CarrierName?.Trim() ?? "", TrackingNumber = r.TrackingNumber?.Trim() ?? "", VehicleId = r.VehicleId, DriverId = r.DriverId, EstimatedDeliveryAtUtc = r.EstimatedDeliveryAtUtc, ProofOfDeliveryUri = r.ProofOfDeliveryUri?.Trim() ?? "" }; db.Shipments.Add(e); await db.SaveChangesAsync(ct); return Results.Created($"/api/operations/shipments/{e.Id}", e);
        });
        g.MapPost("/shipments/{id:guid}/dispatch", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) => await ChangeShipmentStatus(id, ShipmentStatus.Dispatched, tenant, db, ct));
        g.MapPost("/shipments/{id:guid}/deliver", async (Guid id, ICurrentTenant tenant, AppDbContext db, CancellationToken ct) =>
        {
            if (!tenant.IsResolved) return Results.Unauthorized(); var e = await db.Shipments.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct); if (e is null) return Results.NotFound(); if (e.Status != ShipmentStatus.Dispatched && e.Status != ShipmentStatus.InTransit) return Results.Conflict(new { error = "Only dispatched or in-transit shipments can be delivered." }); e.Status = ShipmentStatus.Delivered; e.DeliveredAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Results.Ok(e);
        });

        return app;
    }

    private static async Task<IResult> ChangeOrderStatus(Guid id, OrderStatus next, ICurrentTenant tenant, AppDbContext db, CancellationToken ct)
    {
        if (!tenant.IsResolved) return Results.Unauthorized(); var e = await db.SalesOrders.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct); if (e is null) return Results.NotFound();
        var allowed = (e.Status, next) switch { (OrderStatus.Draft, OrderStatus.Submitted) => true, (OrderStatus.Submitted, OrderStatus.Confirmed) => true, (OrderStatus.Draft, OrderStatus.Cancelled) => true, (OrderStatus.Submitted, OrderStatus.Cancelled) => true, (OrderStatus.Confirmed, OrderStatus.Cancelled) => true, _ => false }; if (!allowed) return Results.Conflict(new { error = $"Cannot transition order from {e.Status} to {next}." }); e.Status = next; await db.SaveChangesAsync(ct); return Results.Ok(e);
    }
    private static async Task<IResult> ChangeShipmentStatus(Guid id, ShipmentStatus next, ICurrentTenant tenant, AppDbContext db, CancellationToken ct)
    {
        if (!tenant.IsResolved) return Results.Unauthorized(); var e = await db.Shipments.SingleOrDefaultAsync(x => x.Id == id && x.TenantId == tenant.Id, ct); if (e is null) return Results.NotFound(); if (e.Status != ShipmentStatus.Planned && e.Status != ShipmentStatus.Prepared) return Results.Conflict(new { error = "Only planned or prepared shipments can be dispatched." }); e.Status = next; await db.SaveChangesAsync(ct); return Results.Ok(e);
    }

    public sealed record FacilityRequest(string Code, string Name, FacilityType Type = FacilityType.Warehouse, string? CountryCode = null, string? City = null, string? Address = null, string? ContactName = null, string? ContactPhone = null, string? ContactEmail = null, decimal? Capacity = null, string? CapacityUnit = null, decimal? Latitude = null, decimal? Longitude = null, bool IsActive = true);
    public sealed record PriceListRequest(string Code, string Name, string? Currency = null, string? CustomerType = null, string? CountryCode = null, DateTime? ValidFromUtc = null, DateTime? ValidToUtc = null, bool IsActive = true);
    public sealed record PriceListItemRequest(Guid CatalogProductId, Guid? ProductVariantId, decimal UnitPrice, string? Sku = null, string? Description = null, string? Unit = null, decimal? MinimumQuantity = null, decimal? MaximumQuantity = null);
    public sealed record CreateOrderRequest(string? Number, Guid? CustomerAccountId, Guid? CompanyId, Guid? ContactId, OrderChannel Channel, string? Currency, DateTime? RequestedDeliveryAtUtc, string? Notes, List<OrderItemRequest> Items);
    public sealed record OrderItemRequest(Guid CatalogProductId, Guid? ProductVariantId, decimal Quantity, decimal UnitPrice, decimal Discount = 0, decimal Tax = 0, string? Sku = null, string? Description = null, string? Unit = null);
    public sealed record InventoryAdjustmentRequest(Guid FacilityId, Guid CatalogProductId, Guid? ProductVariantId, decimal Quantity, string? Unit = null, decimal? MinimumLevel = null);
    public sealed record ReserveInventoryRequest(Guid StockBalanceId, decimal Quantity, Guid? SalesOrderId = null, Guid? FulfillmentId = null, DateTime? ExpiresAtUtc = null);
    public sealed record CreateShipmentRequest(Guid FulfillmentId, string? Number, Guid? FromFacilityId, Guid? ToFacilityId, DeliveryMethod Method = DeliveryMethod.CompanyLogistics, string? CarrierName = null, string? TrackingNumber = null, Guid? VehicleId = null, Guid? DriverId = null, DateTime? EstimatedDeliveryAtUtc = null, string? ProofOfDeliveryUri = null);
}
