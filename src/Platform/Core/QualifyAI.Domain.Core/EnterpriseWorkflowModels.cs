using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace QualifyAI.Domain;

public enum OperationalStockMovementType { Receipt, Production, Reservation, Release, Dispatch, Transfer, Delivery, Adjustment }
public enum DeliveryReceiptStatus { Pending, PartiallyDelivered, Delivered, Rejected }

[Table("StorageBlocks")]
[Index(nameof(TenantId), nameof(FacilityId), nameof(Code), IsUnique = true)]
public sealed class StorageBlock : TenantEntity
{
    [MaxLength(64)] public string Code { get; set; } = string.Empty;
    [MaxLength(256)] public string Name { get; set; } = string.Empty;
    [MaxLength(64)] public string BlockType { get; set; } = "storage";
    public decimal? Capacity { get; set; }
    [MaxLength(32)] public string CapacityUnit { get; set; } = "unit";
    public bool IsActive { get; set; } = true;
}

[Table("StorageStocks")]
[Index(nameof(TenantId), nameof(FacilityId), nameof(StorageBlockId), nameof(CatalogProductId), nameof(ProductVariantId), nameof(LotNumber), IsUnique = true)]
public sealed class StorageStock : TenantEntity
{
    public Guid FacilityId { get; set; }
    public Guid StorageBlockId { get; set; }
    public Guid CatalogProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
    [MaxLength(32)] public string Unit { get; set; } = "unit";
    [MaxLength(128)] public string Barcode { get; set; } = string.Empty;
    [MaxLength(128)] public string LotNumber { get; set; } = string.Empty;
}

[Table("OperationalStockMovements")]
[Index(nameof(TenantId), nameof(IdempotencyKey), IsUnique = true)]
public sealed class OperationalStockMovement : TenantEntity
{
    public Guid FacilityId { get; set; }
    public Guid? StorageBlockId { get; set; }
    public Guid CatalogProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal Quantity { get; set; }
    [MaxLength(32)] public string Unit { get; set; } = "unit";
    public OperationalStockMovementType Type { get; set; }
    [MaxLength(128)] public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    [MaxLength(128)] public string IdempotencyKey { get; set; } = string.Empty;
    [MaxLength(2048)] public string Notes { get; set; } = string.Empty;
}

[Table("Vehicles")]
[Index(nameof(TenantId), nameof(RegistrationNumber), IsUnique = true)]
public sealed class Vehicle : TenantEntity
{
    [MaxLength(64)] public string RegistrationNumber { get; set; } = string.Empty;
    [MaxLength(128)] public string VehicleType { get; set; } = "truck";
    [MaxLength(128)] public string MakeModel { get; set; } = string.Empty;
    public decimal? Capacity { get; set; }
    [MaxLength(32)] public string CapacityUnit { get; set; } = "unit";
    public bool IsActive { get; set; } = true;
}

[Table("Drivers")]
[Index(nameof(TenantId), nameof(LicenseNumber), IsUnique = true)]
public sealed class Driver : TenantEntity
{
    [MaxLength(256)] public string Name { get; set; } = string.Empty;
    [MaxLength(128)] public string LicenseNumber { get; set; } = string.Empty;
    [MaxLength(64)] public string Phone { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

[Table("ShipmentItems")]
[Index(nameof(TenantId), nameof(ShipmentId), nameof(SalesOrderItemId), IsUnique = true)]
public sealed class ShipmentItem : TenantEntity
{
    public Guid ShipmentId { get; set; }
    public Guid SalesOrderItemId { get; set; }
    public Guid CatalogProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public decimal ExpectedQuantity { get; set; }
    public decimal LoadedQuantity { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public decimal DamagedQuantity { get; set; }
    public decimal MissingQuantity { get; set; }
    [MaxLength(32)] public string Unit { get; set; } = "unit";
    [MaxLength(128)] public string Barcode { get; set; } = string.Empty;
}

[Table("DeliveryReceipts")]
[Index(nameof(TenantId), nameof(ShipmentId), IsUnique = true)]
public sealed class DeliveryReceipt : TenantEntity
{
    public Guid ShipmentId { get; set; }
    [MaxLength(128)] public string ReceiptNumber { get; set; } = string.Empty;
    public DeliveryReceiptStatus Status { get; set; } = DeliveryReceiptStatus.Pending;
    [MaxLength(256)] public string RecipientName { get; set; } = string.Empty;
    [MaxLength(256)] public string RecipientCompany { get; set; } = string.Empty;
    [MaxLength(256)] public string RecipientVerification { get; set; } = string.Empty;
    [MaxLength(2048)] public string SignatureUri { get; set; } = string.Empty;
    [MaxLength(2048)] public string ProofOfDeliveryUri { get; set; } = string.Empty;
    [MaxLength(2048)] public string Notes { get; set; } = string.Empty;
    public DateTime? ConfirmedAtUtc { get; set; }
}

[Table("DeliveryReceiptItems")]
[Index(nameof(TenantId), nameof(DeliveryReceiptId), nameof(ShipmentItemId), IsUnique = true)]
public sealed class DeliveryReceiptItem : TenantEntity
{
    public Guid DeliveryReceiptId { get; set; }
    public Guid ShipmentItemId { get; set; }
    public decimal DeliveredQuantity { get; set; }
    public decimal RejectedQuantity { get; set; }
    public decimal DamagedQuantity { get; set; }
    public decimal MissingQuantity { get; set; }
    [MaxLength(32)] public string Unit { get; set; } = "unit";
    [MaxLength(2048)] public string Notes { get; set; } = string.Empty;
}