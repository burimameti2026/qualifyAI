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
        return Ok(new
        {
            facilities = await db.Facilities.CountAsync(x => x.TenantId == id && x.IsActive, ct),
            customers = await db.CustomerAccounts.CountAsync(x => x.TenantId == id && x.IsActive, ct),
            orders = await db.SalesOrders.CountAsync(x => x.TenantId == id, ct),
            pendingOrders = await db.SalesOrders.CountAsync(x => x.TenantId == id && x.Status == OrderStatus.Submitted, ct),
            stockItems = await db.StockBalances.CountAsync(x => x.TenantId == id, ct),
            movementsInTransit = await db.StockMovements.CountAsync(x => x.TenantId == id && x.Status == StockMovementStatus.InTransit, ct),
            shipments = await db.Shipments.CountAsync(x => x.TenantId == id && x.Status != ShipmentStatus.Delivered && x.Status != ShipmentStatus.Cancelled, ct),
            unpaidPayments = await db.Payments.CountAsync(x => x.TenantId == id && x.Status != PaymentStatus.Paid && x.Status != PaymentStatus.Cancelled && x.Status != PaymentStatus.Refunded, ct)
        });
    }

    [HttpGet("facilities")]
    public async Task<IReadOnlyList<Facility>> Facilities(CancellationToken ct) => await db.Facilities.Where(x => x.TenantId == TenantId).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpPost("facilities")]
    public async Task<IActionResult> CreateFacility(Facility input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Code) || string.IsNullOrWhiteSpace(input.Name)) return BadRequest(new { detail = "Facility code and name are required." });
        var id = TenantId;
        if (await db.Facilities.AnyAsync(x => x.TenantId == id && x.Code == input.Code.Trim(), ct)) return Conflict(new { detail = "Facility code already exists." });
        input.Id = Guid.NewGuid(); input.TenantId = id; input.Code = input.Code.Trim(); input.Name = input.Name.Trim(); input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow;
        db.Facilities.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/facilities/{input.Id}", input);
    }

    [HttpPut("facilities/{id:guid}")]
    public async Task<IActionResult> UpdateFacility(Guid id, Facility input, CancellationToken ct)
    {
        var entity = await db.Facilities.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (entity is null) return NotFound();
        entity.Code = input.Code.Trim(); entity.Name = input.Name.Trim(); entity.Type = input.Type; entity.CountryCode = input.CountryCode; entity.City = input.City; entity.Address = input.Address; entity.Latitude = input.Latitude; entity.Longitude = input.Longitude; entity.ContactName = input.ContactName; entity.ContactPhone = input.ContactPhone; entity.ContactEmail = input.ContactEmail; entity.Capacity = input.Capacity; entity.CapacityUnit = input.CapacityUnit; entity.IsActive = input.IsActive; entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Ok(entity);
    }

    [HttpGet("customers")]
    public async Task<IReadOnlyList<CustomerAccount>> Customers(CancellationToken ct) => await db.CustomerAccounts.Where(x => x.TenantId == TenantId).OrderBy(x => x.CompanyId).ToListAsync(ct);

    [HttpGet("price-lists")]
    public async Task<IReadOnlyList<PriceList>> PriceLists(CancellationToken ct) => await db.PriceLists.Where(x => x.TenantId == TenantId).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpGet("orders")]
    public async Task<IReadOnlyList<SalesOrder>> Orders(CancellationToken ct) => await db.SalesOrders.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpGet("orders/{id:guid}")]
    public async Task<IActionResult> Order(Guid id, CancellationToken ct)
    {
        var order = await db.SalesOrders.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (order is null) return NotFound();
        var items = await db.SalesOrderItems.Where(x => x.TenantId == TenantId && x.SalesOrderId == id).ToListAsync(ct);
        return Ok(new { order, items });
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder(SalesOrderRequest request, CancellationToken ct)
    {
        var id = TenantId;
        if (request.Items.Count == 0) return BadRequest(new { detail = "At least one order item is required." });
        if (request.Channel != OrderChannel.B2C && request.CompanyId is null) return BadRequest(new { detail = "Company is required for B2B, distributor and partner orders." });
        var number = $"SO-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24];
        var order = new SalesOrder { Id = Guid.NewGuid(), TenantId = id, Number = number, CustomerAccountId = request.CustomerAccountId, CompanyId = request.CompanyId, ContactId = request.ContactId, Channel = request.Channel, Currency = request.Currency ?? "EUR", Status = OrderStatus.Submitted, Notes = request.Notes ?? string.Empty, RequestedDeliveryAtUtc = request.RequestedDeliveryAtUtc, CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
        foreach (var item in request.Items)
        {
            if (item.Quantity <= 0 || item.UnitPrice < 0) return BadRequest(new { detail = "Order quantities must be positive and prices cannot be negative." });
            var line = new SalesOrderItem { Id = Guid.NewGuid(), TenantId = id, SalesOrderId = order.Id, CatalogProductId = item.CatalogProductId, ProductVariantId = item.ProductVariantId, Sku = item.Sku ?? string.Empty, Description = item.Description ?? string.Empty, Quantity = item.Quantity, Unit = item.Unit ?? "unit", UnitPrice = item.UnitPrice, Discount = item.Discount, Tax = item.Tax, Total = Math.Max(0, item.Quantity * item.UnitPrice - item.Discount + item.Tax), CreatedAtUtc = DateTime.UtcNow, UpdatedAtUtc = DateTime.UtcNow };
            db.SalesOrderItems.Add(line); order.Subtotal += line.Quantity * line.UnitPrice; order.DiscountTotal += line.Discount; order.TaxTotal += line.Tax; order.GrandTotal += line.Total;
        }
        db.SalesOrders.Add(order); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/orders/{order.Id}", order);
    }

    [HttpGet("fulfillments")]
    public async Task<IReadOnlyList<Fulfillment>> Fulfillments(CancellationToken ct) => await db.Fulfillments.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("fulfillments")]
    public async Task<IActionResult> CreateFulfillment(Fulfillment input, CancellationToken ct)
    {
        var id = TenantId; if (!await db.SalesOrders.AnyAsync(x => x.TenantId == id && x.Id == input.SalesOrderId, ct)) return NotFound(new { detail = "Sales order not found." });
        if (input.Type == FulfillmentType.Pickup && input.PickupFacilityId is null) return BadRequest(new { detail = "Pickup facility is required for pickup fulfillment." });
        if (input.Type == FulfillmentType.Delivery && input.DeliveryMethod is null) return BadRequest(new { detail = "Delivery method is required for delivery fulfillment." });
        input.Id = Guid.NewGuid(); input.TenantId = id; input.Status = "pending"; input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.Fulfillments.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/fulfillments/{input.Id}", input);
    }

    [HttpGet("stock")]
    public async Task<IReadOnlyList<StockBalance>> Stock(CancellationToken ct) => await db.StockBalances.Where(x => x.TenantId == TenantId).OrderBy(x => x.FacilityId).ToListAsync(ct);

    [HttpGet("movements")]
    public async Task<IReadOnlyList<StockMovement>> Movements(CancellationToken ct) => await db.StockMovements.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("movements")]
    public async Task<IActionResult> CreateMovement(StockMovement input, CancellationToken ct)
    {
        var id = TenantId; if (input.Quantity <= 0) return BadRequest(new { detail = "Movement quantity must be positive." });
        if (input.FromFacilityId == input.ToFacilityId) return BadRequest(new { detail = "Source and destination facilities must differ." });
        if (input.FromFacilityId is not null && !await db.Facilities.AnyAsync(x => x.TenantId == id && x.Id == input.FromFacilityId && x.IsActive, ct)) return BadRequest(new { detail = "Source facility is invalid." });
        if (input.ToFacilityId is not null && !await db.Facilities.AnyAsync(x => x.TenantId == id && x.Id == input.ToFacilityId && x.IsActive, ct)) return BadRequest(new { detail = "Destination facility is invalid." });
        input.Id = Guid.NewGuid(); input.TenantId = id; input.Number = $"SM-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24]; input.Status = StockMovementStatus.Requested; input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; db.StockMovements.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/movements/{input.Id}", input);
    }

    [HttpPost("movements/{id:guid}/status")]
    public async Task<IActionResult> MovementStatus(Guid id, StockMovementStatus status, CancellationToken ct)
    {
        var movement = await db.StockMovements.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == id, ct); if (movement is null) return NotFound();
        if ((int)status < (int)movement.Status && status != StockMovementStatus.Cancelled) return BadRequest(new { detail = "Stock movement status cannot move backwards." });
        movement.Status = status; movement.UpdatedAtUtc = DateTime.UtcNow; if (status == StockMovementStatus.Dispatched) movement.DispatchedAtUtc = DateTime.UtcNow; if (status == StockMovementStatus.Received || status == StockMovementStatus.Completed) movement.ReceivedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); return Ok(movement);
    }

    [HttpGet("shipments")]
    public async Task<IReadOnlyList<Shipment>> Shipments(CancellationToken ct) => await db.Shipments.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpGet("routes")]
    public async Task<IReadOnlyList<RoutePlan>> Routes(CancellationToken ct) => await db.RoutePlans.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpGet("documents")]
    public async Task<IReadOnlyList<CommercialDocument>> Documents(CancellationToken ct) => await db.CommercialDocuments.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpGet("payments")]
    public async Task<IReadOnlyList<Payment>> Payments(CancellationToken ct) => await db.Payments.Where(x => x.TenantId == TenantId).OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);

    [HttpPost("payments")]
    public async Task<IActionResult> CreatePayment(Payment input, CancellationToken ct)
    {
        var id = TenantId; if (input.Amount <= 0) return BadRequest(new { detail = "Payment amount must be positive." });
        input.Id = Guid.NewGuid(); input.TenantId = id; input.Status = PaymentStatus.Requested; input.CreatedAtUtc = DateTime.UtcNow; input.UpdatedAtUtc = DateTime.UtcNow; input.RequestedAtUtc = DateTime.UtcNow; db.Payments.Add(input); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/payments/{input.Id}", input);
    }
}

public sealed record SalesOrderRequest(Guid? CustomerAccountId, Guid? CompanyId, Guid? ContactId, OrderChannel Channel, string? Currency, DateTime? RequestedDeliveryAtUtc, string? Notes, IReadOnlyList<SalesOrderItemRequest> Items);
public sealed record SalesOrderItemRequest(Guid CatalogProductId, Guid? ProductVariantId, string? Sku, string? Description, decimal Quantity, string? Unit, decimal UnitPrice, decimal Discount, decimal Tax);
