using Microsoft.EntityFrameworkCore;
using QualifyAI.Persistence.SqlServer;

namespace QualifyAI.Api;

public static class BillingSchemaMigration
{
    public static async Task EnsureBillingSchemaAsync(this AppDbContext db, CancellationToken ct = default)
    {
        const string sql = """
        IF OBJECT_ID(N'dbo.BillingEvents', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.BillingEvents (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                Provider nvarchar(64) NOT NULL,
                ExternalEventId nvarchar(256) NOT NULL,
                Type nvarchar(128) NOT NULL,
                TenantId uniqueidentifier NOT NULL,
                Status nvarchar(64) NOT NULL,
                DataJson nvarchar(max) NULL,
                OccurredAtUtc datetime2 NOT NULL,
                RecordedAtUtc datetime2 NOT NULL
            );
            CREATE UNIQUE INDEX IX_BillingEvents_Provider_ExternalEventId ON dbo.BillingEvents(Provider, ExternalEventId);
            CREATE INDEX IX_BillingEvents_TenantId_OccurredAtUtc ON dbo.BillingEvents(TenantId, OccurredAtUtc);
        END

        IF OBJECT_ID(N'dbo.TenantBillingSubscriptions', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.TenantBillingSubscriptions (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                Provider nvarchar(64) NOT NULL,
                ExternalSubscriptionId nvarchar(256) NOT NULL,
                [Plan] nvarchar(128) NOT NULL,
                Status nvarchar(64) NOT NULL,
                StartedAtUtc datetime2 NOT NULL,
                CurrentPeriodEndsAtUtc datetime2 NULL,
                CancelledAtUtc datetime2 NULL,
                UpdatedAtUtc datetime2 NOT NULL
            );
            CREATE UNIQUE INDEX IX_TenantBillingSubscriptions_Provider_ExternalSubscriptionId ON dbo.TenantBillingSubscriptions(Provider, ExternalSubscriptionId);
            CREATE UNIQUE INDEX IX_TenantBillingSubscriptions_TenantId ON dbo.TenantBillingSubscriptions(TenantId);
        END

        IF OBJECT_ID(N'dbo.TenantBillingInvoices', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.TenantBillingInvoices (
                Id uniqueidentifier NOT NULL PRIMARY KEY,
                TenantId uniqueidentifier NOT NULL,
                Provider nvarchar(64) NOT NULL,
                ExternalInvoiceId nvarchar(256) NOT NULL,
                Status nvarchar(64) NOT NULL,
                Currency nvarchar(8) NOT NULL,
                AmountDue decimal(18,2) NOT NULL,
                AmountPaid decimal(18,2) NOT NULL,
                DueAtUtc datetime2 NULL,
                PaidAtUtc datetime2 NULL,
                UpdatedAtUtc datetime2 NOT NULL
            );
            CREATE UNIQUE INDEX IX_TenantBillingInvoices_Provider_ExternalInvoiceId ON dbo.TenantBillingInvoices(Provider, ExternalInvoiceId);
            CREATE INDEX IX_TenantBillingInvoices_TenantId_UpdatedAtUtc ON dbo.TenantBillingInvoices(TenantId, UpdatedAtUtc);
        END

        IF OBJECT_ID(N'dbo.TenantBillingLifecycles', N'U') IS NULL
        BEGIN
            CREATE TABLE dbo.TenantBillingLifecycles (
                TenantId uniqueidentifier NOT NULL PRIMARY KEY,
                State nvarchar(64) NOT NULL,
                TrialEndsAtUtc datetime2 NULL,
                GraceEndsAtUtc datetime2 NULL,
                RetryAttempt int NOT NULL,
                NextRetryAtUtc datetime2 NULL,
                LastPaymentState nvarchar(128) NULL,
                UpdatedAtUtc datetime2 NOT NULL
            );
            CREATE INDEX IX_TenantBillingLifecycles_State_NextRetryAtUtc ON dbo.TenantBillingLifecycles(State, NextRetryAtUtc);
        END
        """;
        await db.Database.ExecuteSqlRawAsync(sql, ct);
    }
}
