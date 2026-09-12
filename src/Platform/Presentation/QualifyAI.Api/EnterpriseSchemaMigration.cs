using Microsoft.EntityFrameworkCore;

namespace QualifyAI.Api;

internal static class EnterpriseSchemaMigration
{
    internal static async Task EnsureEnterpriseSchemaAsync(this DbContext db, CancellationToken ct = default)
    {
        const string sql = @"
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @lockResult int;
EXEC @lockResult = sp_getapplock
    @Resource = N'QualifyAI:EnterpriseSchema',
    @LockMode = N'Exclusive',
    @LockOwner = N'Transaction',
    @LockTimeout = 30000;
IF @lockResult < 0 THROW 51000, 'Unable to acquire Enterprise schema bootstrap lock.', 1;

IF OBJECT_ID(N'dbo.Facilities', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Facilities (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL,
        Code nvarchar(64) NOT NULL, Name nvarchar(256) NOT NULL, Type int NOT NULL,
        CountryCode nvarchar(8) NULL, City nvarchar(128) NULL, Address nvarchar(512) NULL,
        Latitude decimal(18,2) NULL, Longitude decimal(18,2) NULL,
        ContactName nvarchar(max) NULL, ContactPhone nvarchar(max) NULL, ContactEmail nvarchar(320) NULL,
        Capacity decimal(18,2) NULL, CapacityUnit nvarchar(max) NULL, IsActive bit NOT NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_Facilities PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Facilities_TenantId_Code' AND object_id = OBJECT_ID(N'dbo.Facilities'))
    CREATE UNIQUE INDEX IX_Facilities_TenantId_Code ON dbo.Facilities(TenantId, Code);

IF OBJECT_ID(N'dbo.FacilityCapabilities', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FacilityCapabilities (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, FacilityId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NULL, CapabilityType nvarchar(128) NOT NULL, Capacity decimal(18,2) NULL,
        Unit nvarchar(32) NULL, IsActive bit NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_FacilityCapabilities PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FacilityCapabilities_TenantId_FacilityId_CatalogProductId' AND object_id = OBJECT_ID(N'dbo.FacilityCapabilities'))
    CREATE INDEX IX_FacilityCapabilities_TenantId_FacilityId_CatalogProductId ON dbo.FacilityCapabilities(TenantId, FacilityId, CatalogProductId);

IF OBJECT_ID(N'dbo.CustomerAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CustomerAccounts (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, CompanyId uniqueidentifier NOT NULL,
        CustomerType nvarchar(64) NOT NULL, PaymentTerms nvarchar(128) NOT NULL, Currency nvarchar(8) NOT NULL,
        IsActive bit NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_CustomerAccounts PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CustomerAccounts_TenantId_CompanyId' AND object_id = OBJECT_ID(N'dbo.CustomerAccounts'))
    CREATE UNIQUE INDEX IX_CustomerAccounts_TenantId_CompanyId ON dbo.CustomerAccounts(TenantId, CompanyId);

IF OBJECT_ID(N'dbo.PriceLists', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PriceLists (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Code nvarchar(64) NOT NULL,
        Name nvarchar(256) NOT NULL, Currency nvarchar(8) NOT NULL, CustomerType nvarchar(64) NOT NULL,
        CountryCode nvarchar(8) NULL, ValidFromUtc datetime2 NULL, ValidToUtc datetime2 NULL, IsActive bit NOT NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_PriceLists PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PriceLists_TenantId_Code' AND object_id = OBJECT_ID(N'dbo.PriceLists'))
    CREATE UNIQUE INDEX IX_PriceLists_TenantId_Code ON dbo.PriceLists(TenantId, Code);

IF OBJECT_ID(N'dbo.PriceListItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PriceListItems (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, PriceListId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, Sku nvarchar(128) NULL,
        Description nvarchar(1024) NULL, Unit nvarchar(32) NOT NULL, UnitPrice decimal(18,2) NOT NULL,
        MinimumQuantity decimal(18,2) NULL, MaximumQuantity decimal(18,2) NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_PriceListItems PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PriceListItems_TenantId_PriceListId_CatalogProductId_ProductVariantId' AND object_id = OBJECT_ID(N'dbo.PriceListItems'))
    CREATE INDEX IX_PriceListItems_TenantId_PriceListId_CatalogProductId_ProductVariantId ON dbo.PriceListItems(TenantId, PriceListId, CatalogProductId, ProductVariantId);

IF OBJECT_ID(N'dbo.SalesOrders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesOrders (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Number nvarchar(64) NOT NULL,
        CustomerAccountId uniqueidentifier NULL, CompanyId uniqueidentifier NULL, ContactId uniqueidentifier NULL,
        Channel int NOT NULL, Status int NOT NULL, Currency nvarchar(8) NOT NULL,
        Subtotal decimal(18,2) NOT NULL, DiscountTotal decimal(18,2) NOT NULL, TaxTotal decimal(18,2) NOT NULL,
        GrandTotal decimal(18,2) NOT NULL, RequestedDeliveryAtUtc datetime2 NULL, Notes nvarchar(max) NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_SalesOrders PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesOrders_TenantId_Number' AND object_id = OBJECT_ID(N'dbo.SalesOrders'))
    CREATE UNIQUE INDEX IX_SalesOrders_TenantId_Number ON dbo.SalesOrders(TenantId, Number);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesOrders_TenantId_Status_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.SalesOrders'))
    CREATE INDEX IX_SalesOrders_TenantId_Status_CreatedAtUtc ON dbo.SalesOrders(TenantId, Status, CreatedAtUtc);

IF OBJECT_ID(N'dbo.SalesOrderItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesOrderItems (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, SalesOrderId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, Sku nvarchar(128) NULL,
        Description nvarchar(1024) NOT NULL, Quantity decimal(18,2) NOT NULL, Unit nvarchar(32) NOT NULL,
        UnitPrice decimal(18,2) NOT NULL, Discount decimal(18,2) NOT NULL, Tax decimal(18,2) NOT NULL,
        Total decimal(18,2) NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_SalesOrderItems PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SalesOrderItems_TenantId_SalesOrderId' AND object_id = OBJECT_ID(N'dbo.SalesOrderItems'))
    CREATE INDEX IX_SalesOrderItems_TenantId_SalesOrderId ON dbo.SalesOrderItems(TenantId, SalesOrderId);

IF OBJECT_ID(N'dbo.Fulfillments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Fulfillments (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, SalesOrderId uniqueidentifier NOT NULL,
        Type int NOT NULL, DeliveryMethod int NULL, PickupFacilityId uniqueidentifier NULL, OriginFacilityId uniqueidentifier NULL,
        DestinationName nvarchar(256) NULL, DestinationAddress nvarchar(512) NULL, DestinationCountryCode nvarchar(8) NULL,
        DestinationCity nvarchar(128) NULL, DestinationLatitude decimal(18,2) NULL, DestinationLongitude decimal(18,2) NULL,
        Status nvarchar(64) NOT NULL, ScheduledAtUtc datetime2 NULL, CompletedAtUtc datetime2 NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_Fulfillments PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fulfillments_TenantId_SalesOrderId' AND object_id = OBJECT_ID(N'dbo.Fulfillments'))
    CREATE INDEX IX_Fulfillments_TenantId_SalesOrderId ON dbo.Fulfillments(TenantId, SalesOrderId);

IF OBJECT_ID(N'dbo.FulfillmentItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FulfillmentItems (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, FulfillmentId uniqueidentifier NOT NULL,
        SalesOrderItemId uniqueidentifier NOT NULL, Quantity decimal(18,2) NOT NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_FulfillmentItems PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FulfillmentItems_TenantId_FulfillmentId_SalesOrderItemId' AND object_id = OBJECT_ID(N'dbo.FulfillmentItems'))
    CREATE UNIQUE INDEX IX_FulfillmentItems_TenantId_FulfillmentId_SalesOrderItemId ON dbo.FulfillmentItems(TenantId, FulfillmentId, SalesOrderItemId);

IF OBJECT_ID(N'dbo.StockBalances', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockBalances (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, FacilityId uniqueidentifier NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, OnHand decimal(18,2) NOT NULL,
        Reserved decimal(18,2) NOT NULL, MinimumLevel decimal(18,2) NOT NULL, Unit nvarchar(32) NOT NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_StockBalances PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockBalances_TenantId_FacilityId_CatalogProductId_ProductVariantId' AND object_id = OBJECT_ID(N'dbo.StockBalances'))
    CREATE UNIQUE INDEX IX_StockBalances_TenantId_FacilityId_CatalogProductId_ProductVariantId ON dbo.StockBalances(TenantId, FacilityId, CatalogProductId, ProductVariantId);

IF OBJECT_ID(N'dbo.StockReservations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockReservations (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, StockBalanceId uniqueidentifier NOT NULL,
        SalesOrderId uniqueidentifier NULL, FulfillmentId uniqueidentifier NULL, Quantity decimal(18,2) NOT NULL,
        Status nvarchar(32) NOT NULL, ExpiresAtUtc datetime2 NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_StockReservations PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockReservations_TenantId_StockBalanceId_Status' AND object_id = OBJECT_ID(N'dbo.StockReservations'))
    CREATE INDEX IX_StockReservations_TenantId_StockBalanceId_Status ON dbo.StockReservations(TenantId, StockBalanceId, Status);

IF OBJECT_ID(N'dbo.StockMovements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.StockMovements (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Number nvarchar(64) NOT NULL,
        CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, Quantity decimal(18,2) NOT NULL,
        Unit nvarchar(32) NOT NULL, FromFacilityId uniqueidentifier NULL, ToFacilityId uniqueidentifier NULL,
        SalesOrderId uniqueidentifier NULL, FulfillmentId uniqueidentifier NULL, RequestedByUserId uniqueidentifier NULL,
        ApprovedByUserId uniqueidentifier NULL, SentByUserId uniqueidentifier NULL, ReceivedByUserId uniqueidentifier NULL,
        Status int NOT NULL, DispatchedAtUtc datetime2 NULL, ReceivedAtUtc datetime2 NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_StockMovements PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockMovements_TenantId_Number' AND object_id = OBJECT_ID(N'dbo.StockMovements'))
    CREATE UNIQUE INDEX IX_StockMovements_TenantId_Number ON dbo.StockMovements(TenantId, Number);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_StockMovements_TenantId_Status_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.StockMovements'))
    CREATE INDEX IX_StockMovements_TenantId_Status_CreatedAtUtc ON dbo.StockMovements(TenantId, Status, CreatedAtUtc);

IF OBJECT_ID(N'dbo.Shipments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Shipments (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Number nvarchar(64) NOT NULL,
        FulfillmentId uniqueidentifier NOT NULL, FromFacilityId uniqueidentifier NULL, ToFacilityId uniqueidentifier NULL,
        Method int NOT NULL, CarrierName nvarchar(256) NULL, TrackingNumber nvarchar(256) NULL,
        VehicleId uniqueidentifier NULL, DriverId uniqueidentifier NULL, Status int NOT NULL,
        EstimatedDeliveryAtUtc datetime2 NULL, DeliveredAtUtc datetime2 NULL, ProofOfDeliveryUri nvarchar(2048) NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_Shipments PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shipments_TenantId_Number' AND object_id = OBJECT_ID(N'dbo.Shipments'))
    CREATE UNIQUE INDEX IX_Shipments_TenantId_Number ON dbo.Shipments(TenantId, Number);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Shipments_TenantId_Status_EstimatedDeliveryAtUtc' AND object_id = OBJECT_ID(N'dbo.Shipments'))
    CREATE INDEX IX_Shipments_TenantId_Status_EstimatedDeliveryAtUtc ON dbo.Shipments(TenantId, Status, EstimatedDeliveryAtUtc);

IF OBJECT_ID(N'dbo.RoutePlans', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RoutePlans (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, ShipmentId uniqueidentifier NULL,
        Name nvarchar(256) NOT NULL, VehicleId uniqueidentifier NULL, DriverId uniqueidentifier NULL,
        Provider nvarchar(128) NULL, DistanceKm decimal(18,2) NULL, EstimatedMinutes int NULL,
        PlannedStartAtUtc datetime2 NULL, EstimatedArrivalAtUtc datetime2 NULL, Status nvarchar(32) NOT NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_RoutePlans PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RoutePlans_TenantId_Status_PlannedStartAtUtc' AND object_id = OBJECT_ID(N'dbo.RoutePlans'))
    CREATE INDEX IX_RoutePlans_TenantId_Status_PlannedStartAtUtc ON dbo.RoutePlans(TenantId, Status, PlannedStartAtUtc);

IF OBJECT_ID(N'dbo.RouteStops', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.RouteStops (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, RoutePlanId uniqueidentifier NOT NULL,
        Sequence int NOT NULL, FacilityId uniqueidentifier NULL, Name nvarchar(256) NOT NULL, Address nvarchar(512) NULL,
        Latitude decimal(18,2) NULL, Longitude decimal(18,2) NULL, EtaUtc datetime2 NULL, ArrivedAtUtc datetime2 NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_RouteStops PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RouteStops_TenantId_RoutePlanId_Sequence' AND object_id = OBJECT_ID(N'dbo.RouteStops'))
    CREATE UNIQUE INDEX IX_RouteStops_TenantId_RoutePlanId_Sequence ON dbo.RouteStops(TenantId, RoutePlanId, Sequence);

IF OBJECT_ID(N'dbo.CommercialDocuments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CommercialDocuments (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Number nvarchar(64) NOT NULL,
        Type int NOT NULL, SalesOrderId uniqueidentifier NULL, CustomerAccountId uniqueidentifier NULL,
        Amount decimal(18,2) NOT NULL, Currency nvarchar(8) NOT NULL, Status nvarchar(32) NOT NULL,
        IssuedAtUtc datetime2 NULL, DueAtUtc datetime2 NULL, Uri nvarchar(2048) NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_CommercialDocuments PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CommercialDocuments_TenantId_Number' AND object_id = OBJECT_ID(N'dbo.CommercialDocuments'))
    CREATE UNIQUE INDEX IX_CommercialDocuments_TenantId_Number ON dbo.CommercialDocuments(TenantId, Number);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CommercialDocuments_TenantId_Type_Status' AND object_id = OBJECT_ID(N'dbo.CommercialDocuments'))
    CREATE INDEX IX_CommercialDocuments_TenantId_Type_Status ON dbo.CommercialDocuments(TenantId, Type, Status);

IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payments (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, SalesOrderId uniqueidentifier NULL,
        CommercialDocumentId uniqueidentifier NULL, Amount decimal(18,2) NOT NULL, Currency nvarchar(8) NOT NULL,
        Method int NOT NULL, Status int NOT NULL, Provider nvarchar(128) NULL, ExternalTransactionId nvarchar(256) NULL,
        RequestedAtUtc datetime2 NULL, PaidAtUtc datetime2 NULL, Reference nvarchar(256) NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_Payments PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_TenantId_Status_CreatedAtUtc' AND object_id = OBJECT_ID(N'dbo.Payments'))
    CREATE INDEX IX_Payments_TenantId_Status_CreatedAtUtc ON dbo.Payments(TenantId, Status, CreatedAtUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Payments_TenantId_ExternalTransactionId' AND object_id = OBJECT_ID(N'dbo.Payments'))
    CREATE INDEX IX_Payments_TenantId_ExternalTransactionId ON dbo.Payments(TenantId, ExternalTransactionId);

IF OBJECT_ID(N'dbo.PaymentAllocations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PaymentAllocations (
        Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, PaymentId uniqueidentifier NOT NULL,
        CommercialDocumentId uniqueidentifier NOT NULL, Amount decimal(18,2) NOT NULL,
        CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL,
        CONSTRAINT PK_PaymentAllocations PRIMARY KEY (Id));
END;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PaymentAllocations_TenantId_PaymentId_CommercialDocumentId' AND object_id = OBJECT_ID(N'dbo.PaymentAllocations'))
    CREATE UNIQUE INDEX IX_PaymentAllocations_TenantId_PaymentId_CommercialDocumentId ON dbo.PaymentAllocations(TenantId, PaymentId, CommercialDocumentId);

COMMIT TRANSACTION;";

        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
