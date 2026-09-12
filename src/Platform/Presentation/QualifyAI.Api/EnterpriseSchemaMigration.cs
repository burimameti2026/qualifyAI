using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public static class EnterpriseSchemaMigration
{
    public static async Task EnsureEnterpriseSchemaAsync(this AppDbContext db, CancellationToken ct = default)
    {
        const string sql = """
        IF OBJECT_ID(N'dbo.RoutePlans', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.RoutePlans
            (
                Id uniqueidentifier NOT NULL CONSTRAINT PK_RoutePlans PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                ShipmentId uniqueidentifier NULL,
                Name nvarchar(256) NOT NULL,
                VehicleId uniqueidentifier NULL,
                DriverId uniqueidentifier NULL,
                Provider nvarchar(256) NOT NULL,
                DistanceKm decimal(18,3) NULL,
                EstimatedMinutes int NULL,
                PlannedStartAtUtc datetime2 NULL,
                EstimatedArrivalAtUtc datetime2 NULL,
                Status nvarchar(64) NOT NULL,
                CreatedAtUtc datetime2 NOT NULL,
                UpdatedAtUtc datetime2 NOT NULL
            );
            CREATE INDEX IX_RoutePlans_TenantId ON dbo.RoutePlans(TenantId);
            CREATE INDEX IX_RoutePlans_ShipmentId ON dbo.RoutePlans(ShipmentId);
        END;

        IF OBJECT_ID(N'dbo.RouteStops', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.RouteStops
            (
                Id uniqueidentifier NOT NULL CONSTRAINT PK_RouteStops PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                RoutePlanId uniqueidentifier NOT NULL,
                Sequence int NOT NULL,
                FacilityId uniqueidentifier NULL,
                Name nvarchar(256) NOT NULL,
                Address nvarchar(1024) NOT NULL,
                Latitude decimal(18,6) NULL,
                Longitude decimal(18,6) NULL,
                EtaUtc datetime2 NULL,
                ArrivedAtUtc datetime2 NULL,
                CreatedAtUtc datetime2 NOT NULL,
                UpdatedAtUtc datetime2 NOT NULL
            );
            CREATE INDEX IX_RouteStops_TenantId_RoutePlanId ON dbo.RouteStops(TenantId, RoutePlanId);
        END;

        IF OBJECT_ID(N'dbo.CommercialDocuments', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.CommercialDocuments
            (
                Id uniqueidentifier NOT NULL CONSTRAINT PK_CommercialDocuments PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                Number nvarchar(128) NOT NULL,
                Type int NOT NULL,
                SalesOrderId uniqueidentifier NULL,
                CustomerAccountId uniqueidentifier NULL,
                Amount decimal(18,2) NOT NULL,
                Currency nvarchar(8) NOT NULL,
                Status nvarchar(64) NOT NULL,
                IssuedAtUtc datetime2 NULL,
                DueAtUtc datetime2 NULL,
                Uri nvarchar(2048) NOT NULL,
                CreatedAtUtc datetime2 NOT NULL,
                UpdatedAtUtc datetime2 NOT NULL
            );
            CREATE INDEX IX_CommercialDocuments_TenantId ON dbo.CommercialDocuments(TenantId);
            CREATE INDEX IX_CommercialDocuments_SalesOrderId ON dbo.CommercialDocuments(SalesOrderId);
            CREATE INDEX IX_CommercialDocuments_CustomerAccountId ON dbo.CommercialDocuments(CustomerAccountId);
        END;

        IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.Payments
            (
                Id uniqueidentifier NOT NULL CONSTRAINT PK_Payments PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                SalesOrderId uniqueidentifier NULL,
                CommercialDocumentId uniqueidentifier NULL,
                Amount decimal(18,2) NOT NULL,
                Currency nvarchar(8) NOT NULL,
                Method int NOT NULL,
                Status int NOT NULL,
                Provider nvarchar(256) NOT NULL,
                ExternalTransactionId nvarchar(256) NOT NULL,
                RequestedAtUtc datetime2 NULL,
                PaidAtUtc datetime2 NULL,
                Reference nvarchar(256) NOT NULL,
                CreatedAtUtc datetime2 NOT NULL,
                UpdatedAtUtc datetime2 NOT NULL
            );
            CREATE INDEX IX_Payments_TenantId ON dbo.Payments(TenantId);
            CREATE INDEX IX_Payments_SalesOrderId ON dbo.Payments(SalesOrderId);
            CREATE INDEX IX_Payments_CommercialDocumentId ON dbo.Payments(CommercialDocumentId);
        END;

        IF OBJECT_ID(N'dbo.PaymentAllocations', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.PaymentAllocations
            (
                Id uniqueidentifier NOT NULL CONSTRAINT PK_PaymentAllocations PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                PaymentId uniqueidentifier NOT NULL,
                CommercialDocumentId uniqueidentifier NOT NULL,
                Amount decimal(18,2) NOT NULL,
                CreatedAtUtc datetime2 NOT NULL,
                UpdatedAtUtc datetime2 NOT NULL
            );
            CREATE INDEX IX_PaymentAllocations_TenantId_PaymentId ON dbo.PaymentAllocations(TenantId, PaymentId);
            CREATE INDEX IX_PaymentAllocations_CommercialDocumentId ON dbo.PaymentAllocations(CommercialDocumentId);
        END;
        """;

        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
