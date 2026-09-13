using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QualifyAI.Domain;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/enterprise/workflow")]
public sealed class EnterpriseWorkflowController(ITenantContext tenant, AppDbContext db) : ControllerBase
{
    private Guid TenantId => tenant.TenantId();

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var id = TenantId;
        var stock = await db.Set<StorageStock>().Where(x => x.TenantId == id).SumAsync(x => (decimal?)x.Quantity, ct) ?? 0;
        var capacity = await db.Set<StorageBlock>().Where(x => x.TenantId == id && x.IsActive && x.Capacity.HasValue).SumAsync(x => x.Capacity, ct) ?? 0;
        return Ok(new
        {
            facilities = await db.Facilities.CountAsync(x => x.TenantId == id && x.IsActive, ct),
            storageBlocks = await db.Set<StorageBlock>().CountAsync(x => x.TenantId == id && x.IsActive, ct),
            stockQuantity = stock,
            storageCapacity = capacity,
            capacityUtilization = capacity > 0 ? Math.Round(stock / capacity * 100m, 2) : 0,
            orders = await db.SalesOrders.CountAsync(x => x.TenantId == id, ct),
            processing = await db.Fulfillments.CountAsync(x => x.TenantId == id && x.Status != "completed" && x.Status != "delivered" && x.Status != "picked-up", ct),
            inTransit = await db.Shipments.CountAsync(x => x.TenantId == id && x.Status == ShipmentStatus.InTransit, ct),
            deliveries = await db.Set<DeliveryReceipt>().CountAsync(x => x.TenantId == id, ct),
            invoices = await db.CommercialDocuments.CountAsync(x => x.TenantId == id && x.Type == CommercialDocumentType.Invoice, ct)
        });
    }

    [HttpGet("facilities/{facilityId:guid}/blocks")]
    public async Task<IActionResult> Blocks(Guid facilityId, CancellationToken ct)
    {
        if (!await db.Facilities.AnyAsync(x => x.TenantId == TenantId && x.Id == facilityId, ct)) return NotFound();
        var blocks = await db.Set<StorageBlock>().Where(x => x.TenantId == TenantId && x.FacilityId == facilityId).OrderBy(x => x.Code).ToListAsync(ct);
        var stock = await db.Set<StorageStock>().Where(x => x.TenantId == TenantId && x.FacilityId == facilityId).ToListAsync(ct);
        return Ok(blocks.Select(b => new { block = b, quantity = stock.Where(s => s.StorageBlockId == b.Id).Sum(s => s.Quantity), items = stock.Count(s => s.StorageBlockId == b.Id) }));
    }

    [HttpPost("facilities/{facilityId:guid}/blocks")]
    public async Task<IActionResult> CreateBlock(Guid facilityId, StorageBlockRequest request, CancellationToken ct)
    {
        var id = TenantId;
        if (!await db.Facilities.AnyAsync(x => x.TenantId == id && x.Id == facilityId && x.IsActive, ct)) return NotFound(new { detail = "Facility not found." });
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) return BadRequest(new { detail = "Block code and name are required." });
        if (request.Capacity is < 0) return BadRequest(new { detail = "Block capacity cannot be negative." });
        var code = request.Code.Trim();
        if (await db.Set<StorageBlock>().AnyAsync(x => x.TenantId == id && x.FacilityId == facilityId && x.Code == code, ct)) return Conflict(new { detail = "Storage block code already exists." });
        var now = DateTime.UtcNow;
        var block = new StorageBlock { Id = Guid.NewGuid(), TenantId = id, FacilityId = facilityId, Code = code, Name = request.Name.Trim(), BlockType = request.BlockType?.Trim() ?? "storage", Capacity = request.Capacity, CapacityUnit = request.CapacityUnit?.Trim() ?? "unit", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Set<StorageBlock>().Add(block); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/workflow/facilities/{facilityId}/blocks/{block.Id}", block);
    }

    [HttpGet("vehicles")]
    public async Task<IReadOnlyList<Vehicle>> Vehicles(CancellationToken ct) => await db.Set<Vehicle>().Where(x => x.TenantId == TenantId).OrderBy(x => x.RegistrationNumber).ToListAsync(ct);

    [HttpPost("vehicles")]
    public async Task<IActionResult> CreateVehicle(VehicleRequest request, CancellationToken ct)
    {
        var id = TenantId; var registration = request.RegistrationNumber?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(registration)) return BadRequest(new { detail = "Registration number is required." });
        if (await db.Set<Vehicle>().AnyAsync(x => x.TenantId == id && x.RegistrationNumber == registration, ct)) return Conflict(new { detail = "Vehicle already exists." });
        var now = DateTime.UtcNow; var vehicle = new Vehicle { Id = Guid.NewGuid(), TenantId = id, RegistrationNumber = registration, VehicleType = request.VehicleType?.Trim() ?? "truck", MakeModel = request.MakeModel?.Trim() ?? "", Capacity = request.Capacity, CapacityUnit = request.CapacityUnit?.Trim() ?? "unit", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Set<Vehicle>().Add(vehicle); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/workflow/vehicles/{vehicle.Id}", vehicle);
    }

    [HttpGet("drivers")]
    public async Task<IReadOnlyList<Driver>> Drivers(CancellationToken ct) => await db.Set<Driver>().Where(x => x.TenantId == TenantId).OrderBy(x => x.Name).ToListAsync(ct);

    [HttpPost("drivers")]
    public async Task<IActionResult> CreateDriver(DriverRequest request, CancellationToken ct)
    {
        var id = TenantId; if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.LicenseNumber)) return BadRequest(new { detail = "Driver name and license number are required." });
        var license = request.LicenseNumber.Trim(); if (await db.Set<Driver>().AnyAsync(x => x.TenantId == id && x.LicenseNumber == license, ct)) return Conflict(new { detail = "Driver license already exists." });
        var now = DateTime.UtcNow; var driver = new Driver { Id = Guid.NewGuid(), TenantId = id, Name = request.Name.Trim(), LicenseNumber = license, Phone = request.Phone?.Trim() ?? "", IsActive = true, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Set<Driver>().Add(driver); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/workflow/drivers/{driver.Id}", driver);
    }

    [HttpPost("inventory/receive")]
    public async Task<IActionResult> Receive(ReceiveInventoryRequest request, CancellationToken ct)
    {
        var id = TenantId;
        if (request.Quantity <= 0) return BadRequest(new { detail = "Receipt quantity must be positive." });
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) return BadRequest(new { detail = "Idempotency key is required." });
        if (await db.Set<OperationalStockMovement>().AnyAsync(x => x.TenantId == id && x.IdempotencyKey == request.IdempotencyKey, ct)) return Conflict(new { detail = "This inventory receipt was already processed." });
        var facility = await db.Facilities.FirstOrDefaultAsync(x => x.TenantId == id && x.Id == request.FacilityId && x.IsActive, ct);
        var block = await db.Set<StorageBlock>().FirstOrDefaultAsync(x => x.TenantId == id && x.Id == request.StorageBlockId && x.FacilityId == request.FacilityId && x.IsActive, ct);
        if (facility is null || block is null) return NotFound(new { detail = "Facility or storage block not found." });
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        if (block.Capacity.HasValue)
        {
            var current = await db.Set<StorageStock>().Where(x => x.TenantId == id && x.StorageBlockId == block.Id).SumAsync(x => (decimal?)x.Quantity, ct) ?? 0;
            if (current + request.Quantity > block.Capacity.Value) return Conflict(new { detail = "Storage block capacity exceeded.", availableCapacity = block.Capacity.Value - current });
        }
        var now = DateTime.UtcNow; var lot = request.LotNumber?.Trim() ?? "";
        var stock = await db.Set<StorageStock>().FirstOrDefaultAsync(x => x.TenantId == id && x.FacilityId == request.FacilityId && x.StorageBlockId == request.StorageBlockId && x.CatalogProductId == request.CatalogProductId && x.ProductVariantId == request.ProductVariantId && x.LotNumber == lot, ct);
        if (stock is null) { stock = new StorageStock { Id = Guid.NewGuid(), TenantId = id, FacilityId = request.FacilityId, StorageBlockId = request.StorageBlockId, CatalogProductId = request.CatalogProductId, ProductVariantId = request.ProductVariantId, Quantity = 0, Unit = request.Unit?.Trim() ?? "unit", Barcode = request.Barcode?.Trim() ?? "", LotNumber = lot, CreatedAtUtc = now, UpdatedAtUtc = now }; db.Set<StorageStock>().Add(stock); }
        stock.Quantity += request.Quantity; stock.UpdatedAtUtc = now;
        var balance = await db.StockBalances.FirstOrDefaultAsync(x => x.TenantId == id && x.FacilityId == request.FacilityId && x.CatalogProductId == request.CatalogProductId && x.ProductVariantId == request.ProductVariantId, ct);
        if (balance is null) { balance = new StockBalance { Id = Guid.NewGuid(), TenantId = id, FacilityId = request.FacilityId, CatalogProductId = request.CatalogProductId, ProductVariantId = request.ProductVariantId, OnHand = 0, Reserved = 0, MinimumLevel = 0, Unit = stock.Unit, CreatedAtUtc = now, UpdatedAtUtc = now }; db.StockBalances.Add(balance); }
        balance.OnHand += request.Quantity; balance.UpdatedAtUtc = now;
        db.Set<OperationalStockMovement>().Add(new OperationalStockMovement { Id = Guid.NewGuid(), TenantId = id, FacilityId = request.FacilityId, StorageBlockId = request.StorageBlockId, CatalogProductId = request.CatalogProductId, ProductVariantId = request.ProductVariantId, Quantity = request.Quantity, Unit = stock.Unit, Type = OperationalStockMovementType.Receipt, ReferenceType = request.ReferenceType ?? "receipt", ReferenceId = request.ReferenceId, IdempotencyKey = request.IdempotencyKey.Trim(), Notes = request.Notes ?? "", CreatedAtUtc = now, UpdatedAtUtc = now });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(new { stock, balance });
    }

    [HttpPost("dispatch")]
    public async Task<IActionResult> Dispatch(CreateDispatchRequest request, CancellationToken ct)
    {
        var id = TenantId; if (request.Items.Count == 0) return BadRequest(new { detail = "At least one dispatch item is required." });
        var order = await db.SalesOrders.FirstOrDefaultAsync(x => x.TenantId == id && x.Id == request.SalesOrderId, ct); if (order is null) return NotFound(new { detail = "Sales order not found." });
        if (!await db.Facilities.AnyAsync(x => x.TenantId == id && x.Id == request.OriginFacilityId && x.IsActive, ct)) return BadRequest(new { detail = "Origin facility is invalid." });
        if (request.VehicleId.HasValue && !await db.Set<Vehicle>().AnyAsync(x => x.TenantId == id && x.Id == request.VehicleId && x.IsActive, ct)) return BadRequest(new { detail = "Vehicle is invalid." });
        if (request.DriverId.HasValue && !await db.Set<Driver>().AnyAsync(x => x.TenantId == id && x.Id == request.DriverId && x.IsActive, ct)) return BadRequest(new { detail = "Driver is invalid." });
        var orderItems = await db.SalesOrderItems.Where(x => x.TenantId == id && x.SalesOrderId == order.Id).ToListAsync(ct); if (orderItems.Count == 0) return BadRequest(new { detail = "Sales order has no items." });
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        foreach (var requestItem in request.Items)
        {
            if (requestItem.Quantity <= 0) return BadRequest(new { detail = "Dispatch quantities must be positive." });
            var orderItem = orderItems.FirstOrDefault(x => x.Id == requestItem.SalesOrderItemId); if (orderItem is null) return BadRequest(new { detail = "Dispatch item does not belong to the order." });
            var already = await db.Set<ShipmentItem>().Where(x => x.TenantId == id && x.SalesOrderItemId == orderItem.Id).SumAsync(x => (decimal?)x.ExpectedQuantity, ct) ?? 0;
            if (already + requestItem.Quantity > orderItem.Quantity) return Conflict(new { detail = $"Dispatch quantity exceeds ordered quantity for {orderItem.Sku}." });
            var balance = await db.StockBalances.FirstOrDefaultAsync(x => x.TenantId == id && x.FacilityId == request.OriginFacilityId && x.CatalogProductId == orderItem.CatalogProductId && x.ProductVariantId == orderItem.ProductVariantId, ct);
            if (balance is null || balance.Available < requestItem.Quantity) return Conflict(new { detail = $"Insufficient available stock for {orderItem.Sku}." });
            balance.Reserved += requestItem.Quantity; balance.UpdatedAtUtc = DateTime.UtcNow;
        }
        var now = DateTime.UtcNow;
        var fulfillment = new Fulfillment { Id = Guid.NewGuid(), TenantId = id, SalesOrderId = order.Id, Type = FulfillmentType.Delivery, DeliveryMethod = DeliveryMethod.CompanyLogistics, OriginFacilityId = request.OriginFacilityId, DestinationName = request.DestinationName?.Trim() ?? "", DestinationAddress = request.DestinationAddress?.Trim() ?? "", DestinationCountryCode = request.DestinationCountryCode?.Trim() ?? "", DestinationCity = request.DestinationCity?.Trim() ?? "", Status = "prepared", ScheduledAtUtc = request.ScheduledAtUtc, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Fulfillments.Add(fulfillment);
        var shipment = new Shipment { Id = Guid.NewGuid(), TenantId = id, Number = $"SH-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24], FulfillmentId = fulfillment.Id, FromFacilityId = request.OriginFacilityId, Method = DeliveryMethod.CompanyLogistics, VehicleId = request.VehicleId, DriverId = request.DriverId, Status = ShipmentStatus.Prepared, EstimatedDeliveryAtUtc = request.EstimatedDeliveryAtUtc, CreatedAtUtc = now, UpdatedAtUtc = now };
        db.Shipments.Add(shipment);
        foreach (var requestItem in request.Items)
        {
            var orderItem = orderItems.Single(x => x.Id == requestItem.SalesOrderItemId);
            db.FulfillmentItems.Add(new FulfillmentItem { Id = Guid.NewGuid(), TenantId = id, FulfillmentId = fulfillment.Id, SalesOrderItemId = orderItem.Id, Quantity = requestItem.Quantity, CreatedAtUtc = now, UpdatedAtUtc = now });
            db.Set<ShipmentItem>().Add(new ShipmentItem { Id = Guid.NewGuid(), TenantId = id, ShipmentId = shipment.Id, SalesOrderItemId = orderItem.Id, CatalogProductId = orderItem.CatalogProductId, ProductVariantId = orderItem.ProductVariantId, ExpectedQuantity = requestItem.Quantity, Unit = orderItem.Unit, Barcode = requestItem.Barcode?.Trim() ?? orderItem.Sku, CreatedAtUtc = now, UpdatedAtUtc = now });
        }
        order.Status = OrderStatus.Confirmed; order.UpdatedAtUtc = now; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Created($"/api/enterprise/workflow/dispatch/{shipment.Id}", new { fulfillment, shipment });
    }

    [HttpPost("dispatch/{shipmentId:guid}/load")]
    public async Task<IActionResult> Load(Guid shipmentId, LoadDispatchRequest request, CancellationToken ct)
    {
        var id = TenantId; var shipment = await db.Shipments.FirstOrDefaultAsync(x => x.TenantId == id && x.Id == shipmentId, ct); if (shipment is null) return NotFound();
        if (shipment.Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled) return Conflict(new { detail = "Shipment is already closed." });
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        foreach (var itemRequest in request.Items)
        {
            if (itemRequest.Quantity <= 0) return BadRequest(new { detail = "Loaded quantity must be positive." });
            var item = await db.Set<ShipmentItem>().FirstOrDefaultAsync(x => x.TenantId == id && x.Id == itemRequest.ShipmentItemId && x.ShipmentId == shipmentId, ct); if (item is null) return NotFound(new { detail = "Shipment item not found." });
            if (item.LoadedQuantity + itemRequest.Quantity > item.ExpectedQuantity) return Conflict(new { detail = "Loaded quantity exceeds expected quantity." });
            var balance = await db.StockBalances.FirstOrDefaultAsync(x => x.TenantId == id && x.FacilityId == shipment.FromFacilityId && x.CatalogProductId == item.CatalogProductId && x.ProductVariantId == item.ProductVariantId, ct);
            if (balance is null || balance.Available < itemRequest.Quantity) return Conflict(new { detail = "Insufficient available stock." });
            var storage = await db.Set<StorageStock>().Where(x => x.TenantId == id && x.FacilityId == shipment.FromFacilityId && x.CatalogProductId == item.CatalogProductId && x.ProductVariantId == item.ProductVariantId && x.Quantity > 0).OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
            var remaining = itemRequest.Quantity; var now = DateTime.UtcNow;
            foreach (var slot in storage)
            {
                if (remaining <= 0) break; var take = Math.Min(slot.Quantity, remaining); slot.Quantity -= take; slot.UpdatedAtUtc = now; remaining -= take;
                db.Set<OperationalStockMovement>().Add(new OperationalStockMovement { Id = Guid.NewGuid(), TenantId = id, FacilityId = shipment.FromFacilityId!.Value, StorageBlockId = slot.StorageBlockId, CatalogProductId = item.CatalogProductId, ProductVariantId = item.ProductVariantId, Quantity = take, Unit = slot.Unit, Type = OperationalStockMovementType.Dispatch, ReferenceType = "shipment", ReferenceId = shipmentId, IdempotencyKey = $"load:{shipmentId}:{item.Id}:{item.LoadedQuantity + itemRequest.Quantity}:{slot.Id}", Notes = "", CreatedAtUtc = now, UpdatedAtUtc = now });
            }
            if (remaining > 0) return Conflict(new { detail = "Physical storage stock is insufficient." });
            balance.OnHand -= itemRequest.Quantity; balance.Reserved = Math.Max(0, balance.Reserved - itemRequest.Quantity); balance.UpdatedAtUtc = now; item.LoadedQuantity += itemRequest.Quantity; item.UpdatedAtUtc = now;
        }
        shipment.Status = ShipmentStatus.Dispatched; shipment.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(shipment);
    }

    [HttpPost("dispatch/{shipmentId:guid}/status")]
    public async Task<IActionResult> ShipmentStatus(Guid shipmentId, ShipmentStatus status, CancellationToken ct)
    {
        var entity = await db.Shipments.FirstOrDefaultAsync(x => x.TenantId == TenantId && x.Id == shipmentId, ct); if (entity is null) return NotFound();
        if (status == ShipmentStatus.InTransit && entity.Status != ShipmentStatus.Dispatched) return BadRequest(new { detail = "Shipment must be dispatched before entering transit." });
        if (status == ShipmentStatus.Delivered) return BadRequest(new { detail = "Use delivery confirmation to close the shipment." });
        if ((int)status < (int)entity.Status && status != ShipmentStatus.Cancelled) return BadRequest(new { detail = "Shipment status cannot move backwards." });
        entity.Status = status; entity.UpdatedAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); return Ok(entity);
    }

    [HttpPost("deliveries/confirm")]
    public async Task<IActionResult> ConfirmDelivery(ConfirmDeliveryRequest request, CancellationToken ct)
    {
        var id = TenantId; var shipment = await db.Shipments.FirstOrDefaultAsync(x => x.TenantId == id && x.Id == request.ShipmentId, ct); if (shipment is null) return NotFound(new { detail = "Shipment not found." });
        if (shipment.Status is ShipmentStatus.Delivered or ShipmentStatus.Cancelled) return Conflict(new { detail = "Shipment is already closed." });
        if (await db.Set<DeliveryReceipt>().AnyAsync(x => x.TenantId == id && x.ShipmentId == shipment.Id, ct)) return Conflict(new { detail = "Delivery confirmation already exists." });
        var items = await db.Set<ShipmentItem>().Where(x => x.TenantId == id && x.ShipmentId == shipment.Id).ToListAsync(ct); if (items.Count == 0) return BadRequest(new { detail = "Shipment has no items." });
        if (string.IsNullOrWhiteSpace(request.RecipientName) || string.IsNullOrWhiteSpace(request.RecipientVerification)) return BadRequest(new { detail = "Recipient name and verification are required." });
        await using var tx = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable, ct);
        var now = DateTime.UtcNow; var receipt = new DeliveryReceipt { Id = Guid.NewGuid(), TenantId = id, ShipmentId = shipment.Id, ReceiptNumber = $"DR-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24], RecipientName = request.RecipientName.Trim(), RecipientCompany = request.RecipientCompany?.Trim() ?? "", RecipientVerification = request.RecipientVerification.Trim(), SignatureUri = request.SignatureUri?.Trim() ?? "", ProofOfDeliveryUri = request.ProofOfDeliveryUri?.Trim() ?? "", Notes = request.Notes?.Trim() ?? "", CreatedAtUtc = now, UpdatedAtUtc = now };
        foreach (var item in items)
        {
            var input = request.Items.FirstOrDefault(x => x.ShipmentItemId == item.Id); if (input is null) return BadRequest(new { detail = "Every shipment item must be reconciled." });
            if (input.DeliveredQuantity < 0 || input.RejectedQuantity < 0 || input.DamagedQuantity < 0 || input.MissingQuantity < 0) return BadRequest(new { detail = "Delivery quantities cannot be negative." });
            if (input.DeliveredQuantity + input.RejectedQuantity + input.DamagedQuantity + input.MissingQuantity != item.LoadedQuantity) return Conflict(new { detail = "Delivery quantities must reconcile exactly to loaded quantity." });
            item.DeliveredQuantity = input.DeliveredQuantity; item.RejectedQuantity = input.RejectedQuantity; item.DamagedQuantity = input.DamagedQuantity; item.MissingQuantity = input.MissingQuantity; item.UpdatedAtUtc = now;
            db.Set<DeliveryReceiptItem>().Add(new DeliveryReceiptItem { Id = Guid.NewGuid(), TenantId = id, DeliveryReceiptId = receipt.Id, ShipmentItemId = item.Id, DeliveredQuantity = input.DeliveredQuantity, RejectedQuantity = input.RejectedQuantity, DamagedQuantity = input.DamagedQuantity, MissingQuantity = input.MissingQuantity, Unit = item.Unit, Notes = input.Notes ?? "", CreatedAtUtc = now, UpdatedAtUtc = now });
        }
        var fullyDelivered = items.All(x => x.DeliveredQuantity == x.ExpectedQuantity);
        receipt.Status = fullyDelivered ? DeliveryReceiptStatus.Delivered : DeliveryReceiptStatus.PartiallyDelivered; receipt.ConfirmedAtUtc = now; shipment.Status = fullyDelivered ? ShipmentStatus.Delivered : ShipmentStatus.InTransit; shipment.DeliveredAtUtc = fullyDelivered ? now : null; shipment.ProofOfDeliveryUri = receipt.ProofOfDeliveryUri; shipment.UpdatedAtUtc = now;
        db.Set<DeliveryReceipt>().Add(receipt);
        var fulfillment = await db.Fulfillments.FirstAsync(x => x.TenantId == id && x.Id == shipment.FulfillmentId, ct); fulfillment.Status = fullyDelivered ? "delivered" : "partial"; fulfillment.CompletedAtUtc = fullyDelivered ? now : null; fulfillment.UpdatedAtUtc = now;
        var order = await db.SalesOrders.FirstAsync(x => x.TenantId == id && x.Id == fulfillment.SalesOrderId, ct); order.Status = fullyDelivered ? OrderStatus.Fulfilled : OrderStatus.PartiallyFulfilled; order.UpdatedAtUtc = now;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Ok(new { receipt, shipment, order });
    }

    [HttpPost("orders/{orderId:guid}/invoice")]
    public async Task<IActionResult> Invoice(Guid orderId, CancellationToken ct)
    {
        var id = TenantId; var order = await db.SalesOrders.FirstOrDefaultAsync(x => x.TenantId == id && x.Id == orderId, ct); if (order is null) return NotFound();
        if (order.Status != OrderStatus.Fulfilled) return Conflict(new { detail = "Order must be fully delivered before invoicing." });
        var existing = await db.CommercialDocuments.FirstOrDefaultAsync(x => x.TenantId == id && x.SalesOrderId == orderId && x.Type == CommercialDocumentType.Invoice, ct); if (existing is not null) return Ok(existing);
        var now = DateTime.UtcNow; var invoice = new CommercialDocument { Id = Guid.NewGuid(), TenantId = id, Number = $"INV-{now:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..24], Type = CommercialDocumentType.Invoice, SalesOrderId = order.Id, CustomerAccountId = order.CustomerAccountId, Amount = order.GrandTotal, Currency = order.Currency, Status = "issued", IssuedAtUtc = now, DueAtUtc = now.AddDays(30), CreatedAtUtc = now, UpdatedAtUtc = now };
        db.CommercialDocuments.Add(invoice); await db.SaveChangesAsync(ct); return Created($"/api/enterprise/workflow/orders/{orderId}/invoice", invoice);
    }
}

public sealed record StorageBlockRequest(string? Code, string? Name, string? BlockType, decimal? Capacity, string? CapacityUnit);
public sealed record VehicleRequest(string? RegistrationNumber, string? VehicleType, string? MakeModel, decimal? Capacity, string? CapacityUnit);
public sealed record DriverRequest(string? Name, string? LicenseNumber, string? Phone);
public sealed record ReceiveInventoryRequest(Guid FacilityId, Guid StorageBlockId, Guid CatalogProductId, Guid? ProductVariantId, decimal Quantity, string? Unit, string? Barcode, string? LotNumber, string IdempotencyKey, string? ReferenceType, Guid? ReferenceId, string? Notes);
public sealed record DispatchItemRequest(Guid SalesOrderItemId, decimal Quantity, string? Barcode);
public sealed record CreateDispatchRequest(Guid SalesOrderId, Guid OriginFacilityId, Guid? VehicleId, Guid? DriverId, string? DestinationName, string? DestinationAddress, string? DestinationCountryCode, string? DestinationCity, DateTime? ScheduledAtUtc, DateTime? EstimatedDeliveryAtUtc, IReadOnlyList<DispatchItemRequest> Items);
public sealed record LoadItemRequest(Guid ShipmentItemId, decimal Quantity);
public sealed record LoadDispatchRequest(IReadOnlyList<LoadItemRequest> Items);
public sealed record DeliveryReconciliationItem(Guid ShipmentItemId, decimal DeliveredQuantity, decimal RejectedQuantity, decimal DamagedQuantity, decimal MissingQuantity, string? Notes);
public sealed record ConfirmDeliveryRequest(Guid ShipmentId, string? RecipientName, string? RecipientCompany, string? RecipientVerification, string? SignatureUri, string? ProofOfDeliveryUri, string? Notes, IReadOnlyList<DeliveryReconciliationItem> Items);