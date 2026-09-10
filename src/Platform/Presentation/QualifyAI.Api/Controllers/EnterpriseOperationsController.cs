using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.BuildingBlocks.Security.Access;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/enterprise")]
public sealed class EnterpriseOperationsController(ITenantContext tenant, AppDbContext db) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("overview")]
    public async Task<IActionResult> Overview(CancellationToken ct)
    {
        var id = TenantId;
        return Ok(new { facilities = await db.Facilities.CountAsync(x => x.TenantId == id && x.IsActive, ct), customers = await db.CustomerAccounts.CountAsync(x => x.TenantId == id && x.IsActive, ct), orders = await db.SalesOrders.CountAsync(x => x.TenantId == id, ct), pendingOrders = await db.SalesOrders.CountAsync(x => x.TenantId == id && x.Status == OrderStatus.Submitted, ct), stockItems = await db.StockBalances.CountAsync(x => x.TenantId == id, ct), movementsInTransit = await db.StockMovements.CountAsync(x => x.TenantId == id && x.Status == StockMovementStatus.InTransit, ct), shipments = await db.Shipments.CountAsync(x => x.TenantId == id && x.Status != ShipmentStatus.Delivered && x.Status != ShipmentStatus.Cancelled, ct), unpaidPayments = await db.Payments.CountAsync(x => x.TenantId == id && x.Status != PaymentStatus.Paid && x.Status != PaymentStatus.Cancelled && x.Status != PaymentStatus.Refunded, ct) });
    }

    [HttpGet("facilities")]
    public async Task<IReadOnlyList<Facility>> Facilities(CancellationToken ct) => await db.Facilities.Where(x => x.TenantId == TenantId).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpPost("facilities")]
    public async Task<IActionResult> CreateFacility(Facility input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { detail = "Facility code and name are required." });
        var id = TenantId; input.Code = input.Code.Trim(); if (await db.Facilities.AnyAsync(x => x.TenantId == id && x.Code == input.Code, ct)) return Conflict(new { detail = "Facility code already exists." });
        input.Id = Guid.NewGuid(); input.TenantId = id; input.Name = input.Name.Trim(); input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.Facilities.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/facilities/{input.Id}", input);
    }

    [HttpPut("facilities/{id:guid}")]
    public async Task<IActionResult> UpdateFacility(Guid id, Facility input, CancellationToken ct)
    {
        var entity = await db.Facilities.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (entity is null) return NotFound();
        entity.Code = input.Code.Trim(); entity.Name = input.Name.Trim(); entity.Type = input.Type; entity.CountryCode = input.CountryCode; entity.City = input.City; entity.Address = input.Address; entity.Latitude = input.Latitude; entity.Longitude = input.Longitude; entity.ContactName = input.ContactName; entity.ContactPhone = input.ContactPhone; entity.ContactEmail = input.ContactEmail; entity.Capacity = input.Capacity; entity.CapacityUnit = input.CapacityUnit; entity.IsActive = input.IsActive; entity.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(entity);
    }

    [HttpGet("facilities/{facilityId:guid}/capabilities")]
    public async Task<IReadOnlyList<FacilityCapability>> Capabilities(Guid facilityId, CancellationToken ct) => await db.FacilityCapabilities.Where(x => x.TenantId == TenantId && x.FacilityId == facilityId).ToListAsync(ct);

    [HttpPost("facilities/{facilityId:guid}/capabilities")]
    public async Task<IActionResult> AddCapability(Guid facilityId, FacilityCapability input, CancellationToken ct)
    {
        var id = TenantId; if (!await db.Facilities.AnyAsync(x => x.TenantId == id && x.Id == facilityId, ct)) return NotFound(); input.Id = Guid.NewGuid(); input.TenantId = id; input.FacilityId = facilityId; input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.FacilityCapabilities.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/facilities/{facilityId}/capabilities/{input.Id}", input);
    }

    [HttpGet("customers")]
    public async Task<IReadOnlyList<CustomerAccount>> Customers(CancellationToken ct) => await db.CustomerAccounts.Where(x => x.TenantId == TenantId).OrderBy(x => x.CompanyId).ToListAsync(ct);

    [HttpPost("customers")]
    public async Task<IActionResult> CreateCustomer(CustomerAccount input, CancellationToken ct)
    {
        var id = TenantId; if (input.CompanyId == Guid.Empty) return BadRequest(new { detail = "Company is required." }); if (await db.CustomerAccounts.AnyAsync(x => x.TenantId == id && x.CompanyId == input.CompanyId, ct)) return Conflict(new { detail = "Customer account already exists for this company." }); input.Id = Guid.NewGuid(); input.TenantId = id; input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.CustomerAccounts.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/customers/{input.Id}", input);
    }

    [HttpGet("price-lists")]
    public async Task<IReadOnlyList<PriceList>> PriceLists(CancellationToken ct) => await db.PriceLists.Where(x => x.TenantId == TenantId).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpPost("price-lists")]
    public async Task<IActionResult> CreatePriceList(PriceList input, CancellationToken ct)
    {
        var id = TenantId; if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { detail = "Price list code and name are required." }); input.Id = Guid.NewGuid(); input.TenantId = id; input.Code = input.Code.Trim(); input.Name = input.Name.Trim(); input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.PriceLists.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/price-lists/{input.Id}", input);
    }

    [HttpPost("price-lists/{priceListId:guid}/items")]
    public async Task<IActionResult> AddPriceItem(Guid priceListId, PriceListItem input, CancellationToken ct)
    {
        var id = TenantId; if (!await db.PriceLists.AnyAsync(x => x.TenantId == id && x.Id == priceListId, ct)) return NotFound(); if (input.UnitPrice < 0) return BadRequest(new { detail = "Unit price cannot be negative." }); input.Id = Guid.NewGuid(); input.TenantId = id; input.PriceListId = priceListId; input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.PriceListItems.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/price-lists/{priceListId}/items/{input.Id}", input);
    }

    [HttpGet("orders")]
    public async Task<IReadOnlyList<SalesOrder>> Orders(CancellationToken ct) => await db.SalesOrders.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken ct)
    {
        var order = await db.SalesOrders.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (order is null) return NotFound(); var items = await db.SalesOrderItems.Where(x => x.TenantId == TenantId && x.SalesOrderId == id).ToListAsync(ct); return Ok(new { order, items });
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(SalesOrderRequest request, CancellationToken ct)
    {
        var id = TenantId; if (request.Items.Count == 0) return BadRequest(new { detail = "At least one order item is required." }); if (request.Channel != OrderChannel.B2C && request.CompanyId is null) return BadRequest(new { detail = "Company is required for B2B, distributor and partner orders." }); var now = DateTime.UtcNow; var order = new SalesOrder { Id = Guid.NewGuid(), TenantId = id, Number = $"SO-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24], CustomerAccountId = request.CustomerAccountId, CompanyId = request.CompanyId, ContactId = request.ContactId, Channel = request.Channel, Currency = request.Currency ?? "EUR", Status = OrderStatus.Submitted, Notes = request.Notes ?? string.Empty, RequestedDeliveryAtUtc = request.RequestedDeliveryAtUtc, CreatedAtUtc = now, UpdatedAtUtc = now };
        foreach (var item in request.Items)
        { if (item.Quantity <= 0 || item.UnitPrice < 0 || item.Discount < 0 || item.Tax < 0) return BadRequest(new { detail = "Order quantity, price, discount and tax values are invalid." }); var line = new SalesOrderItem { Id = Guid.NewGuid(), TenantId = id, SalesOrderId = order.Id, CatalogProductId = item.CatalogProductId, ProductVariantId = item.ProductVariantId, Sku = item.Sku ?? string.Empty, Description = item.Description ?? string.Empty, Quantity = item.Quantity, Unit = item.Unit ?? "unit", UnitPrice = item.UnitPrice, Discount = item.Discount, Tax = item.Tax, Total = Math.Max(0, item.Quantity * item.UnitPrice - item.Discount + item.Tax), CreatedAtUtc = now, UpdatedAtUtc = now }; db.SalesOrderItems.Add(line); order.Subtotal += line.Quantity * line.UnitPrice; order.DiscountTotal += line.Discount; order.TaxTotal += line.Tax; order.GrandTotal += line.Total; }
        db.SalesOrders.Add(order); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/orders/{order.Id}", order);
    }

    [HttpPost("orders/{id:guid}/status")]
    public async Task<IActionResult> OrderStatus(Guid id, OrderStatus status, CancellationToken ct)
    { var order = await db.SalesOrders.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (order is null) return NotFound(); if ((int)status < (int)order.Status && status != QualifyAI.Domain.OrderStatus.Cancelled) return BadRequest(new { detail = "Order status cannot move backwards." }); order.Status = status; order.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(order); }

    [HttpGet("fulfillments")]
    public async Task<IReadOnlyList<Fulfillment>> Fulfillments(CancellationToken ct) => await db.Fulfillments.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("fulfillments")]
    public async Task<IActionResult> CreateFulfillment(Fulfillment input, CancellationToken ct)
    { var id = TenantId; if (!await db.SalesOrders.AnyAsync(x => x.TenantId == id && x.Id == input.SalesOrderId, ct)) return NotFound(new { detail = "Sales order not found." }); if (input.Type == FulfillmentType.Pickup && input.PickupFacilityId is null) return BadRequest(new { detail = "Pickup facility is required." }); if (input.Type == FulfillmentType.Delivery && input.DeliveryMethod is null) return BadRequest(new { detail = "Delivery method is required." }); input.Id = Guid.NewGuid(); input.TenantId = id; input.Status = "pending"; input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.Fulfillments.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/fulfillments/{input.Id}", input); }

    [HttpPost("fulfillments/{id:guid}/status")]
    public async Task<IActionResult> FulfillmentStatus(Guid id, string status, CancellationToken ct)
    { var entity = await db.Fulfillments.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (entity is null) return NotFound(); if (string.IsNullOrWhiteSpace(status)) return BadRequest(); entity.Status = status.Trim().ToLowerInvariant(); entity.UpdatedAtUtc = DateTime.UtcNow; if (entity.Status is "completed" or "delivered" or "picked-up") entity.CompletedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(entity); }

    [HttpGet("stock")]
    public async Task<IReadOnlyList<StockBalance>> Stock(CancellationToken ct) => await db.StockBalances.Where(x => x.TenantId == TenantId).OrderBy(x => x.FacilityId).ToListAsync(ct);

    [HttpGet("reservations")]
    public async Task<IReadOnlyList<StockReservation>> Reservations(CancellationToken ct) => await db.StockReservations.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("reservations")]
    public async Task<IActionResult> Reserve(StockReservation input, CancellationToken ct)
    { var id = TenantId; if (input.Quantity <= 0) return BadRequest(new { detail = "Reservation quantity must be positive." }); var stock = await db.StockBalances.FirstOrDefaultAsync(x => x.TenantId == id && x.Id == input.StockBalanceId, ct); if (stock is null) return NotFound(new { detail = "Stock balance not found." }); if (stock.Available < input.Quantity) return Conflict(new { detail = "Insufficient available stock." }); var now = DateTime.UtcNow; input.Id = Guid.NewGuid(); input.TenantId = id; input.Status = "reserved"; input.CreatedAtUtc = now; input.UpdatedAtUtc = now; stock.Reserved += input.Quantity; stock.UpdatedAtUtc = now; db.StockReservations.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/reservations/{input.Id}", input); }

    [HttpGet("movements")]
    public async Task<IReadOnlyList<StockMovement>> Movements(CancellationToken ct) => await db.StockMovements.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("movements")]
    public async Task<IActionResult> CreateMovement(StockMovement input, CancellationToken ct)
    { var id = TenantId; if (input.Quantity <= 0) return BadRequest(new { detail = "Movement quantity must be positive." }); if (input.FromFacilityId == input.ToFacilityId) return BadRequest(new { detail = "Source and destination must differ." }); if (input.FromFacilityId is not null && !await db.Facilities.AnyAsync(x => x.TenantId == id && x.Id == input.FromFacilityId && x.IsActive, ct)) return BadRequest(new { detail = "Source facility is invalid." }); if (input.ToFacilityId is not null && !await db.Facilities.AnyAsync(x => x.TenantId == id && x.Id == input.ToFacilityId && x.IsActive, ct)) return BadRequest(new { detail = "Destination facility is invalid." }); var now = DateTime.UtcNow; input.Id = Guid.NewGuid(); input.TenantId = id; input.Number = $"SM-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24]; input.Status = StockMovementStatus.Requested; input.CreatedAtUtc = now; input.UpdatedAtUtc = now; db.StockMovements.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/movements/{input.Id}", input); }

    [HttpPost("movements/{id:guid}/status")]
    public async Task<IActionResult> MovementStatus(Guid id, StockMovementStatus status, CancellationToken ct)
    { var movement = await db.StockMovements.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (movement is null) return NotFound(); if ((int)status < (int)movement.Status && status != StockMovementStatus.Cancelled) return BadRequest(new { detail = "Stock movement status cannot move backwards." }); movement.Status = status; movement.UpdatedAtUtc = DateTime.UtcNow; if (status == StockMovementStatus.Dispatched) movement.DispatchedAtUtc = DateTime.UtcNow; if (status is StockMovementStatus.Received or StockMovementStatus.Completed) movement.ReceivedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(movement); }

    [HttpGet("shipments")]
    public async Task<IReadOnlyList<Shipment>> Shipments(CancellationToken ct) => await db.Shipments.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("shipments")]
    public async Task<IActionResult> CreateShipment(Shipment input, CancellationToken ct)
    { var id = TenantId; if (!await db.Fulfillments.AnyAsync(x => x.TenantId == id && x.Id == input.FulfillmentId, ct)) return NotFound(new { detail = "Fulfillment not found." }); var now = DateTime.UtcNow; input.Id = Guid.NewGuid(); input.TenantId = id; input.Number = $"SH-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24]; input.Status = ShipmentStatus.Planned; input.CreatedAtUtc = now; input.UpdatedAtUtc = now; db.Shipments.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/shipments/{input.Id}", input); }

    [HttpPost("shipments/{id:guid}/status")]
    public async Task<IActionResult> ShipmentStatus(Guid id, ShipmentStatus status, CancellationToken ct)
    { var entity = await db.Shipments.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (entity is null) return NotFound(); if ((int)status < (int)entity.Status && status != ShipmentStatus.Cancelled) return BadRequest(new { detail = "Shipment status cannot move backwards." }); entity.Status = status; entity.UpdatedAtUtc = DateTime.UtcNow; if (status == ShipmentStatus.Delivered) entity.DeliveredAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(entity); }

    [HttpGet("routes")]
    public async Task<IReadOnlyList<RoutePlan>> Routes(CancellationToken ct) => await db.RoutePlans.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("routes")]
    public async Task<IActionResult> CreateRoute(RoutePlan input, CancellationToken ct)
    { var id = TenantId; if (input.ShipmentId is not null && !await db.Shipments.AnyAsync(x => x.TenantId == id && x.Id == input.ShipmentId, ct)) return NotFound(new { detail = "Shipment not found." }); input.Id = Guid.NewGuid(); input.TenantId = id; input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.RoutePlans.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/routes/{input.Id}", input); }

    [HttpPost("routes/{routeId:guid}/stops")]
    public async Task<IActionResult> AddRouteStop(Guid routeId, RouteStop input, CancellationToken ct)
    { var id = TenantId; if (!await db.RoutePlans.AnyAsync(x => x.TenantId == id && x.Id == routeId, ct)) return NotFound(); if (input.Sequence < 1) return BadRequest(new { detail = "Route sequence must start at 1." }); input.Id = Guid.NewGuid(); input.TenantId = id; input.RoutePlanId = routeId; input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.RouteStops.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/routes/{routeId}/stops/{input.Id}", input); }

    [HttpGet("documents")]
    public async Task<IReadOnlyList<CommercialDocument>> Documents(CancellationToken ct) => await db.CommercialDocuments.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("documents")]
    public async Task<IActionResult> CreateDocument(CommercialDocument input, CancellationToken ct)
    { var id = TenantId; if (input.Amount < 0) return BadRequest(new { detail = "Document amount cannot be negative." }); var now = DateTime.UtcNow; input.Id = Guid.NewGuid(); input.TenantId = id; input.Number = $"DOC-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24]; input.Status = "draft"; input.CreatedAtUtc = now; input.UpdatedAtUtc = now; db.CommercialDocuments.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/documents/{input.Id}", input); }

    [HttpGet("payments")]
    public async Task<IReadOnlyList<Payment>> Payments(CancellationToken ct) => await db.Payments.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("payments")]
    public async Task<IActionResult> CreatePayment(Payment input, CancellationToken ct)
    { var id = TenantId; if (input.Amount <= 0) return BadRequest(new { detail = "Payment amount must be positive." }); var now = DateTime.UtcNow; input.Id = Guid.NewGuid(); input.TenantId = id; input.Status = PaymentStatus.Requested; input.CreatedAtUtc = now; input.UpdatedAtUtc = now; input.RequestedAtUtc = now; db.Payments.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/payments/{input.Id}", input); }

    [HttpPost("payments/{id:guid}/status")]
    public async Task<IActionResult> PaymentStatus(Guid id, PaymentStatus status, CancellationToken ct)
    { var payment = await db.Payments.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (payment is null) return NotFound(); payment.Status = status; payment.UpdatedAtUtc = DateTime.UtcNow; if (status == QualifyAI.Domain.PaymentStatus.Paid) payment.PaidAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(payment); }
}

public sealed record SalesOrderRequest(Guid? CustomerAccountId, Guid? CompanyId, Guid? ContactId, OrderChannel Channel, string? Currency, DateTime? RequestedDeliveryAtUtc, string? Notes, IReadOnlyList<SalesOrderItemRequest> Items);
public sealed record SalesOrderItemRequest(Guid CatalogProductId, Guid? ProductVariantId, string? Sku, string? Description, decimal Quantity, string? Unit, decimal UnitPrice, decimal Discount, decimal Tax);
