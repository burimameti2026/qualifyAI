using Microsoft.EntityFrameworkCore;

namespace QualifyAI.Api;

internal static class EnterpriseWorkflowSchemaMigration
{
    internal static async Task EnsureEnterpriseWorkflowSchemaAsync(this DbContext db, CancellationToken ct = default)
    {
        const string sql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;
DECLARE @lockResult int;
EXEC @lockResult = sp_getapplock @Resource=N'QualifyAI:EnterpriseWorkflowSchema', @LockMode=N'Exclusive', @LockOwner=N'Transaction', @LockTimeout=30000;
IF @lockResult < 0 THROW 51001, 'Unable to acquire Enterprise workflow schema bootstrap lock.', 1;

IF OBJECT_ID(N'dbo.StorageBlocks', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.StorageBlocks(
  Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, FacilityId uniqueidentifier NOT NULL,
  Code nvarchar(64) NOT NULL, Name nvarchar(256) NOT NULL, BlockType nvarchar(64) NOT NULL,
  Capacity decimal(18,2) NULL, CapacityUnit nvarchar(32) NOT NULL, IsActive bit NOT NULL,
  CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
  CONSTRAINT PK_StorageBlocks PRIMARY KEY(Id));
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_StorageBlocks_Tenant_Facility_Code' AND object_id=OBJECT_ID(N'dbo.StorageBlocks')) CREATE UNIQUE INDEX IX_StorageBlocks_Tenant_Facility_Code ON dbo.StorageBlocks(TenantId,FacilityId,Code);

IF OBJECT_ID(N'dbo.StorageStocks', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.StorageStocks(
  Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, FacilityId uniqueidentifier NOT NULL, StorageBlockId uniqueidentifier NOT NULL,
  CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, Quantity decimal(18,2) NOT NULL,
  Unit nvarchar(32) NOT NULL, Barcode nvarchar(128) NOT NULL, LotNumber nvarchar(128) NOT NULL,
  CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
  CONSTRAINT PK_StorageStocks PRIMARY KEY(Id));
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_StorageStocks_Tenant_Location_Item_Lot' AND object_id=OBJECT_ID(N'dbo.StorageStocks')) CREATE UNIQUE INDEX IX_StorageStocks_Tenant_Location_Item_Lot ON dbo.StorageStocks(TenantId,FacilityId,StorageBlockId,CatalogProductId,ProductVariantId,LotNumber);

IF OBJECT_ID(N'dbo.OperationalStockMovements', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.OperationalStockMovements(
  Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, FacilityId uniqueidentifier NOT NULL, StorageBlockId uniqueidentifier NULL,
  CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, Quantity decimal(18,2) NOT NULL, Unit nvarchar(32) NOT NULL,
  Type int NOT NULL, ReferenceType nvarchar(128) NOT NULL, ReferenceId uniqueidentifier NULL, IdempotencyKey nvarchar(128) NOT NULL, Notes nvarchar(2048) NOT NULL,
  CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
  CONSTRAINT PK_OperationalStockMovements PRIMARY KEY(Id));
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_OperationalStockMovements_Tenant_IdempotencyKey' AND object_id=OBJECT_ID(N'dbo.OperationalStockMovements')) CREATE UNIQUE INDEX IX_OperationalStockMovements_Tenant_IdempotencyKey ON dbo.OperationalStockMovements(TenantId,IdempotencyKey);

IF OBJECT_ID(N'dbo.Vehicles', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.Vehicles(
  Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, RegistrationNumber nvarchar(64) NOT NULL, VehicleType nvarchar(128) NOT NULL,
  MakeModel nvarchar(128) NOT NULL, Capacity decimal(18,2) NULL, CapacityUnit nvarchar(32) NOT NULL, IsActive bit NOT NULL,
  CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_Vehicles PRIMARY KEY(Id));
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Vehicles_Tenant_RegistrationNumber' AND object_id=OBJECT_ID(N'dbo.Vehicles')) CREATE UNIQUE INDEX IX_Vehicles_Tenant_RegistrationNumber ON dbo.Vehicles(TenantId,RegistrationNumber);

IF OBJECT_ID(N'dbo.Drivers', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.Drivers(
  Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Name nvarchar(256) NOT NULL, LicenseNumber nvarchar(128) NOT NULL,
  Phone nvarchar(64) NOT NULL, IsActive bit NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
  CONSTRAINT PK_Drivers PRIMARY KEY(Id));
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_Drivers_Tenant_LicenseNumber' AND object_id=OBJECT_ID(N'dbo.Drivers')) CREATE UNIQUE INDEX IX_Drivers_Tenant_LicenseNumber ON dbo.Drivers(TenantId,LicenseNumber);

IF OBJECT_ID(N'dbo.ShipmentItems', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.ShipmentItems(
  Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, ShipmentId uniqueidentifier NOT NULL, SalesOrderItemId uniqueidentifier NOT NULL,
  CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, ExpectedQuantity decimal(18,2) NOT NULL, LoadedQuantity decimal(18,2) NOT NULL,
  DeliveredQuantity decimal(18,2) NOT NULL, RejectedQuantity decimal(18,2) NOT NULL, DamagedQuantity decimal(18,2) NOT NULL, MissingQuantity decimal(18,2) NOT NULL,
  Unit nvarchar(32) NOT NULL, Barcode nvarchar(128) NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
  CONSTRAINT PK_ShipmentItems PRIMARY KEY(Id));
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_ShipmentItems_Tenant_Shipment_OrderItem' AND object_id=OBJECT_ID(N'dbo.ShipmentItems')) CREATE UNIQUE INDEX IX_ShipmentItems_Tenant_Shipment_OrderItem ON dbo.ShipmentItems(TenantId,ShipmentId,SalesOrderItemId);

IF OBJECT_ID(N'dbo.DeliveryReceipts', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.DeliveryReceipts(
  Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, ShipmentId uniqueidentifier NOT NULL, ReceiptNumber nvarchar(128) NOT NULL,
  Status int NOT NULL, RecipientName nvarchar(256) NOT NULL, RecipientCompany nvarchar(256) NOT NULL, RecipientVerification nvarchar(256) NOT NULL,
  SignatureUri nvarchar(2048) NOT NULL, ProofOfDeliveryUri nvarchar(2048) NOT NULL, Notes nvarchar(2048) NOT NULL, ConfirmedAtUtc datetime2 NULL,
  CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_DeliveryReceipts PRIMARY KEY(Id));
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_DeliveryReceipts_Tenant_Shipment' AND object_id=OBJECT_ID(N'dbo.DeliveryReceipts')) CREATE UNIQUE INDEX IX_DeliveryReceipts_Tenant_Shipment ON dbo.DeliveryReceipts(TenantId,ShipmentId);

IF OBJECT_ID(N'dbo.DeliveryReceiptItems', N'U') IS NULL
BEGIN
 CREATE TABLE dbo.DeliveryReceiptItems(
  Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, DeliveryReceiptId uniqueidentifier NOT NULL, ShipmentItemId uniqueidentifier NOT NULL,
  DeliveredQuantity decimal(18,2) NOT NULL, RejectedQuantity decimal(18,2) NOT NULL, DamagedQuantity decimal(18,2) NOT NULL, MissingQuantity decimal(18,2) NOT NULL,
  Unit nvarchar(32) NOT NULL, Notes nvarchar(2048) NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
  CONSTRAINT PK_DeliveryReceiptItems PRIMARY KEY(Id));
END;
IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE name=N'IX_DeliveryReceiptItems_Tenant_Receipt_ShipmentItem' AND object_id=OBJECT_ID(N'dbo.DeliveryReceiptItems')) CREATE UNIQUE INDEX IX_DeliveryReceiptItems_Tenant_Receipt_ShipmentItem ON dbo.DeliveryReceiptItems(TenantId,DeliveryReceiptId,ShipmentItemId);

COMMIT TRANSACTION;";
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}