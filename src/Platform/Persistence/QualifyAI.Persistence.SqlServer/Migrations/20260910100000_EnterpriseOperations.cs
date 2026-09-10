using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Persistence.SqlServer.Migrations;

[Migration("20260910100000_EnterpriseOperations")]
public partial class EnterpriseOperations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
CREATE TABLE dbo.Facilities (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Code nvarchar(64) NOT NULL, Name nvarchar(256) NOT NULL, Type int NOT NULL, CountryCode nvarchar(8) NULL, City nvarchar(128) NULL, Address nvarchar(512) NULL, Latitude decimal(18,2) NULL, Longitude decimal(18,2) NULL, ContactName nvarchar(max) NULL, ContactPhone nvarchar(max) NULL, ContactEmail nvarchar(320) NULL, Capacity decimal(18,2) NULL, CapacityUnit nvarchar(max) NULL, IsActive bit NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_Facilities PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_Facilities_TenantId_Code ON dbo.Facilities(TenantId, Code);

CREATE TABLE dbo.FacilityCapabilities (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, FacilityId uniqueidentifier NOT NULL, CatalogProductId uniqueidentifier NULL, CapabilityType nvarchar(128) NOT NULL, Capacity decimal(18,2) NULL, Unit nvarchar(32) NULL, IsActive bit NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_FacilityCapabilities PRIMARY KEY (Id));
CREATE INDEX IX_FacilityCapabilities_TenantId_FacilityId_CatalogProductId ON dbo.FacilityCapabilities(TenantId, FacilityId, CatalogProductId);

CREATE TABLE dbo.CustomerAccounts (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, CompanyId uniqueidentifier NOT NULL, CustomerType nvarchar(64) NOT NULL, PaymentTerms nvarchar(128) NOT NULL, Currency nvarchar(8) NOT NULL, IsActive bit NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_CustomerAccounts PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_CustomerAccounts_TenantId_CompanyId ON dbo.CustomerAccounts(TenantId, CompanyId);

CREATE TABLE dbo.PriceLists (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Code nvarchar(64) NOT NULL, Name nvarchar(256) NOT NULL, Currency nvarchar(8) NOT NULL, CustomerType nvarchar(64) NOT NULL, CountryCode nvarchar(8) NULL, ValidFromUtc datetime2 NULL, ValidToUtc datetime2 NULL, IsActive bit NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_PriceLists PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_PriceLists_TenantId_Code ON dbo.PriceLists(TenantId, Code);

CREATE TABLE dbo.PriceListItems (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, PriceListId uniqueidentifier NOT NULL, CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, Sku nvarchar(128) NULL, Description nvarchar(1024) NULL, Unit nvarchar(32) NOT NULL, UnitPrice decimal(18,2) NOT NULL, MinimumQuantity decimal(18,2) NULL, MaximumQuantity decimal(18,2) NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_PriceListItems PRIMARY KEY (Id));
CREATE INDEX IX_PriceListItems_TenantId_PriceListId_CatalogProductId_ProductVariantId ON dbo.PriceListItems(TenantId, PriceListId, CatalogProductId, ProductVariantId);

CREATE TABLE dbo.SalesOrders (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Number nvarchar(64) NOT NULL, CustomerAccountId uniqueidentifier NULL, CompanyId uniqueidentifier NULL, ContactId uniqueidentifier NULL, Channel int NOT NULL, Status int NOT NULL, Currency nvarchar(8) NOT NULL, Subtotal decimal(18,2) NOT NULL, DiscountTotal decimal(18,2) NOT NULL, TaxTotal decimal(18,2) NOT NULL, GrandTotal decimal(18,2) NOT NULL, RequestedDeliveryAtUtc datetime2 NULL, Notes nvarchar(max) NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_SalesOrders PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_SalesOrders_TenantId_Number ON dbo.SalesOrders(TenantId, Number);
CREATE INDEX IX_SalesOrders_TenantId_Status_CreatedAtUtc ON dbo.SalesOrders(TenantId, Status, CreatedAtUtc);

CREATE TABLE dbo.SalesOrderItems (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, SalesOrderId uniqueidentifier NOT NULL, CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, Sku nvarchar(128) NULL, Description nvarchar(1024) NOT NULL, Quantity decimal(18,2) NOT NULL, Unit nvarchar(32) NOT NULL, UnitPrice decimal(18,2) NOT NULL, Discount decimal(18,2) NOT NULL, Tax decimal(18,2) NOT NULL, Total decimal(18,2) NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_SalesOrderItems PRIMARY KEY (Id));
CREATE INDEX IX_SalesOrderItems_TenantId_SalesOrderId ON dbo.SalesOrderItems(TenantId, SalesOrderId);

CREATE TABLE dbo.Fulfillments (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, SalesOrderId uniqueidentifier NOT NULL, Type int NOT NULL, DeliveryMethod int NULL, PickupFacilityId uniqueidentifier NULL, OriginFacilityId uniqueidentifier NULL, DestinationName nvarchar(256) NULL, DestinationAddress nvarchar(512) NULL, DestinationCountryCode nvarchar(8) NULL, DestinationCity nvarchar(128) NULL, DestinationLatitude decimal(18,2) NULL, DestinationLongitude decimal(18,2) NULL, Status nvarchar(64) NOT NULL, ScheduledAtUtc datetime2 NULL, CompletedAtUtc datetime2 NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_Fulfillments PRIMARY KEY (Id));
CREATE INDEX IX_Fulfillments_TenantId_SalesOrderId ON dbo.Fulfillments(TenantId, SalesOrderId);

CREATE TABLE dbo.FulfillmentItems (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, FulfillmentId uniqueidentifier NOT NULL, SalesOrderItemId uniqueidentifier NOT NULL, Quantity decimal(18,2) NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_FulfillmentItems PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_FulfillmentItems_TenantId_FulfillmentId_SalesOrderItemId ON dbo.FulfillmentItems(TenantId, FulfillmentId, SalesOrderItemId);

CREATE TABLE dbo.StockBalances (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, FacilityId uniqueidentifier NOT NULL, CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, OnHand decimal(18,2) NOT NULL, Reserved decimal(18,2) NOT NULL, MinimumLevel decimal(18,2) NOT NULL, Unit nvarchar(32) NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_StockBalances PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_StockBalances_TenantId_FacilityId_CatalogProductId_ProductVariantId ON dbo.StockBalances(TenantId, FacilityId, CatalogProductId, ProductVariantId);

CREATE TABLE dbo.StockReservations (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, StockBalanceId uniqueidentifier NOT NULL, SalesOrderId uniqueidentifier NULL, FulfillmentId uniqueidentifier NULL, Quantity decimal(18,2) NOT NULL, Status nvarchar(32) NOT NULL, ExpiresAtUtc datetime2 NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_StockReservations PRIMARY KEY (Id));
CREATE INDEX IX_StockReservations_TenantId_StockBalanceId_Status ON dbo.StockReservations(TenantId, StockBalanceId, Status);

CREATE TABLE dbo.StockMovements (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Number nvarchar(64) NOT NULL, CatalogProductId uniqueidentifier NOT NULL, ProductVariantId uniqueidentifier NULL, Quantity decimal(18,2) NOT NULL, Unit nvarchar(32) NOT NULL, FromFacilityId uniqueidentifier NULL, ToFacilityId uniqueidentifier NULL, SalesOrderId uniqueidentifier NULL, FulfillmentId uniqueidentifier NULL, RequestedByUserId uniqueidentifier NULL, ApprovedByUserId uniqueidentifier NULL, SentByUserId uniqueidentifier NULL, ReceivedByUserId uniqueidentifier NULL, Status int NOT NULL, DispatchedAtUtc datetime2 NULL, ReceivedAtUtc datetime2 NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_StockMovements PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_StockMovements_TenantId_Number ON dbo.StockMovements(TenantId, Number);
CREATE INDEX IX_StockMovements_TenantId_Status_CreatedAtUtc ON dbo.StockMovements(TenantId, Status, CreatedAtUtc);

CREATE TABLE dbo.Shipments (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Number nvarchar(64) NOT NULL, FulfillmentId uniqueidentifier NOT NULL, FromFacilityId uniqueidentifier NULL, ToFacilityId uniqueidentifier NULL, Method int NOT NULL, CarrierName nvarchar(256) NULL, TrackingNumber nvarchar(256) NULL, VehicleId uniqueidentifier NULL, DriverId uniqueidentifier NULL, Status int NOT NULL, EstimatedDeliveryAtUtc datetime2 NULL, DeliveredAtUtc datetime2 NULL, ProofOfDeliveryUri nvarchar(2048) NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_Shipments PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_Shipments_TenantId_Number ON dbo.Shipments(TenantId, Number);
CREATE INDEX IX_Shipments_TenantId_Status_EstimatedDeliveryAtUtc ON dbo.Shipments(TenantId, Status, EstimatedDeliveryAtUtc);

CREATE TABLE dbo.RoutePlans (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, ShipmentId uniqueidentifier NULL, Name nvarchar(256) NOT NULL, VehicleId uniqueidentifier NULL, DriverId uniqueidentifier NULL, Provider nvarchar(128) NULL, DistanceKm decimal(18,2) NULL, EstimatedMinutes int NULL, PlannedStartAtUtc datetime2 NULL, EstimatedArrivalAtUtc datetime2 NULL, Status nvarchar(32) NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_RoutePlans PRIMARY KEY (Id));
CREATE INDEX IX_RoutePlans_TenantId_Status_PlannedStartAtUtc ON dbo.RoutePlans(TenantId, Status, PlannedStartAtUtc);

CREATE TABLE dbo.RouteStops (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, RoutePlanId uniqueidentifier NOT NULL, Sequence int NOT NULL, FacilityId uniqueidentifier NULL, Name nvarchar(256) NOT NULL, Address nvarchar(512) NULL, Latitude decimal(18,2) NULL, Longitude decimal(18,2) NULL, EtaUtc datetime2 NULL, ArrivedAtUtc datetime2 NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_RouteStops PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_RouteStops_TenantId_RoutePlanId_Sequence ON dbo.RouteStops(TenantId, RoutePlanId, Sequence);

CREATE TABLE dbo.CommercialDocuments (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, Number nvarchar(64) NOT NULL, Type int NOT NULL, SalesOrderId uniqueidentifier NULL, CustomerAccountId uniqueidentifier NULL, Amount decimal(18,2) NOT NULL, Currency nvarchar(8) NOT NULL, Status nvarchar(32) NOT NULL, IssuedAtUtc datetime2 NULL, DueAtUtc datetime2 NULL, Uri nvarchar(2048) NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_CommercialDocuments PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_CommercialDocuments_TenantId_Number ON dbo.CommercialDocuments(TenantId, Number);
CREATE INDEX IX_CommercialDocuments_TenantId_Type_Status ON dbo.CommercialDocuments(TenantId, Type, Status);

CREATE TABLE dbo.Payments (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, SalesOrderId uniqueidentifier NULL, CommercialDocumentId uniqueidentifier NULL, Amount decimal(18,2) NOT NULL, Currency nvarchar(8) NOT NULL, Method int NOT NULL, Status int NOT NULL, Provider nvarchar(128) NULL, ExternalTransactionId nvarchar(256) NULL, RequestedAtUtc datetime2 NULL, PaidAtUtc datetime2 NULL, Reference nvarchar(256) NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_Payments PRIMARY KEY (Id));
CREATE INDEX IX_Payments_TenantId_Status_CreatedAtUtc ON dbo.Payments(TenantId, Status, CreatedAtUtc);
CREATE INDEX IX_Payments_TenantId_ExternalTransactionId ON dbo.Payments(TenantId, ExternalTransactionId);

CREATE TABLE dbo.PaymentAllocations (Id uniqueidentifier NOT NULL, TenantId uniqueidentifier NOT NULL, PaymentId uniqueidentifier NOT NULL, CommercialDocumentId uniqueidentifier NOT NULL, Amount decimal(18,2) NOT NULL, CreatedAtUtc datetime2 NOT NULL, UpdatedAtUtc datetime2 NOT NULL, CONSTRAINT PK_PaymentAllocations PRIMARY KEY (Id));
CREATE UNIQUE INDEX IX_PaymentAllocations_TenantId_PaymentId_CommercialDocumentId ON dbo.PaymentAllocations(TenantId, PaymentId, CommercialDocumentId);
");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(@"
DROP TABLE IF EXISTS dbo.PaymentAllocations;
DROP TABLE IF EXISTS dbo.Payments;
DROP TABLE IF EXISTS dbo.CommercialDocuments;
DROP TABLE IF EXISTS dbo.RouteStops;
DROP TABLE IF EXISTS dbo.RoutePlans;
DROP TABLE IF EXISTS dbo.Shipments;
DROP TABLE IF EXISTS dbo.StockMovements;
DROP TABLE IF EXISTS dbo.StockReservations;
DROP TABLE IF EXISTS dbo.StockBalances;
DROP TABLE IF EXISTS dbo.FulfillmentItems;
DROP TABLE IF EXISTS dbo.Fulfillments;
DROP TABLE IF EXISTS dbo.SalesOrderItems;
DROP TABLE IF EXISTS dbo.SalesOrders;
DROP TABLE IF EXISTS dbo.PriceListItems;
DROP TABLE IF EXISTS dbo.PriceLists;
DROP TABLE IF EXISTS dbo.CustomerAccounts;
DROP TABLE IF EXISTS dbo.FacilityCapabilities;
DROP TABLE IF EXISTS dbo.Facilities;
");
    }
}
