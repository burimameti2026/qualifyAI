namespace QualifyAI.Domain;

public enum FacilityType { Factory, Warehouse, DistributionPoint, Office, Other }
public enum OrderChannel { B2C, B2B, Distributor, Partner }
public enum FulfillmentType { Pickup, Delivery }
public enum DeliveryMethod { CompanyLogistics, ExternalCarrier }
public enum StockMovementStatus { Requested, Approved, Prepared, Dispatched, InTransit, Received, Completed, Cancelled }
public enum OrderStatus { Draft, Submitted, Confirmed, PartiallyFulfilled, Fulfilled, Cancelled }
public enum PaymentMethod { Cash, BankTransfer, Card, Online, Other }
public enum PaymentStatus { Pending, Requested, PartiallyPaid, Paid, Failed, Refunded, Cancelled }
public enum CommercialDocumentType { Proforma, Invoice, Receipt, CreditNote }
public enum ShipmentStatus { Planned, Prepared, Dispatched, InTransit, Delivered, Cancelled }

public sealed class Facility : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public FacilityType Type { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public decimal? Capacity { get; set; }
    public string CapacityUnit { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class FacilityCapability : TenantEntity
{
    public Guid FacilityId { get; set; }
    public Guid? CatalogProductId { get; set; }
    public string CapabilityType { get; set; } = string.Empty;
    public decimal? Capacity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

public sealed class CustomerAccount : TenantEntity
{
    public Guid CompanyId { get; set; }
    public string CustomerType { get; set; } = "customer";
    public string PaymentTerms { get; set; } = "due-on-receipt";
    public string Currency { get; set; } = "EUR";
    public bool IsActive { get; set; } = true;
}

public sealed class PriceList : TenantEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "EUR";
    public string CustomerType { get; set; } = "default";
    public string CountryCode { get; set; } = string.Empty;
    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class PriceListItem : TenantEntity
{
    public Guid PriceListId { get; set; }
    public Guid CatalogProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Unit { get; set; } = "unit";
    public decimal UnitPrice { get; set; }
    public decimal? MinimumQuantity { get; set; }
    public decimal? MaximumQuantity { get; set; }
}

public sealed class SalesOrder : TenantEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid? CustomerAccountId { get; set; }
    public Guid? CompanyId { get; set; }
    public Guid? ContactId { get; set; }
    public OrderChannel Channel { get; set; } = OrderChannel.B2C;
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public string Currency { get; set; } = "EUR";
    public decimal Subtotal { get; set; }
    public decimal DiscountTotal { get; set; }
    public decimal TaxTotal { get; set; }
    public decimal GrandTotal { get; set; }
    public DateTime? RequestedDeliveryAtUtc { get; set; }
    public string Notes { get; set; } = string.Empty;
}

public sealed class SalesOrderItem : TenantEntity
{
    public Guid SalesOrderId { get; set; }
    public Guid CatalogProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "unit";
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
}

public sealed class Fulfillment : TenantEntity
{
    public Guid SalesOrderId { get; set; }
    public FulfillmentType Type { get; set; }
    public DeliveryMethod? DeliveryMethod { get; set; }
    public Guid? PickupFacilityId { get; set; }
    public Guid? OriginFacilityId { get; set; }
    public string DestinationName { get; set; } = string.Empty;
    public string DestinationAddress { get; set; } = string.Empty;
    public string DestinationCountryCode { get; set; } = string.Empty;
    public string DestinationCity { get; set; } = string.Empty;
    public decimal? DestinationLatitude { get; set; }
    public decimal? DestinationLongitude { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime? ScheduledAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
}

public sealed class FulfillmentItem : TenantEntity
{
    public Guid FulfillmentId { get; set; }
    public Guid SalesOrderItemId { get; set; }
    public decimal Quantity { get; set; }
}

public sealed class StockBalance : TenantEntity
{
    public Guid FacilityId { get; set; }
    public Guid CatalogProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal OnHand { get; set; }
    public decimal Reserved { get; set; }
    public decimal Available => Math.Max(0, OnHand - Reserved);
    public decimal MinimumLevel { get; set; }
    public string Unit { get; set; } = "unit";
}

public sealed class StockReservation : TenantEntity
{
    public Guid StockBalanceId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? FulfillmentId { get; set; }
    public decimal Quantity { get; set; }
    public string Status { get; set; } = "reserved";
    public DateTime? ExpiresAtUtc { get; set; }
}

public sealed class StockMovement : TenantEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid CatalogProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "unit";
    public Guid? FromFacilityId { get; set; }
    public Guid? ToFacilityId { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? FulfillmentId { get; set; }
    public Guid? RequestedByUserId { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public Guid? SentByUserId { get; set; }
    public Guid? ReceivedByUserId { get; set; }
    public StockMovementStatus Status { get; set; } = StockMovementStatus.Requested;
    public DateTime? DispatchedAtUtc { get; set; }
    public DateTime? ReceivedAtUtc { get; set; }
}

public sealed class Shipment : TenantEntity
{
    public string Number { get; set; } = string.Empty;
    public Guid FulfillmentId { get; set; }
    public Guid? FromFacilityId { get; set; }
    public Guid? ToFacilityId { get; set; }
    public DeliveryMethod Method { get; set; } = DeliveryMethod.CompanyLogistics;
    public string CarrierName { get; set; } = string.Empty;
    public string TrackingNumber { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public ShipmentStatus Status { get; set; } = ShipmentStatus.Planned;
    public DateTime? EstimatedDeliveryAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public string ProofOfDeliveryUri { get; set; } = string.Empty;
}

public sealed class RoutePlan : TenantEntity
{
    public Guid? ShipmentId { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? VehicleId { get; set; }
    public Guid? DriverId { get; set; }
    public string Provider { get; set; } = string.Empty;
    public decimal? DistanceKm { get; set; }
    public int? EstimatedMinutes { get; set; }
    public DateTime? PlannedStartAtUtc { get; set; }
    public DateTime? EstimatedArrivalAtUtc { get; set; }
    public string Status { get; set; } = "planned";
}

public sealed class RouteStop : TenantEntity
{
    public Guid RoutePlanId { get; set; }
    public int Sequence { get; set; }
    public Guid? FacilityId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime? EtaUtc { get; set; }
    public DateTime? ArrivedAtUtc { get; set; }
}

public sealed class CommercialDocument : TenantEntity
{
    public string Number { get; set; } = string.Empty;
    public CommercialDocumentType Type { get; set; }
    public Guid? SalesOrderId { get; set; }
    public Guid? CustomerAccountId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public string Status { get; set; } = "draft";
    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public string Uri { get; set; } = string.Empty;
}

public sealed class Payment : TenantEntity
{
    public Guid? SalesOrderId { get; set; }
    public Guid? CommercialDocumentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "EUR";
    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public string Provider { get; set; } = string.Empty;
    public string ExternalTransactionId { get; set; } = string.Empty;
    public DateTime? RequestedAtUtc { get; set; }
    public DateTime? PaidAtUtc { get; set; }
    public string Reference { get; set; } = string.Empty;
}

public sealed class PaymentAllocation : TenantEntity
{
    public Guid PaymentId { get; set; }
    public Guid CommercialDocumentId { get; set; }
    public decimal Amount { get; set; }
}
