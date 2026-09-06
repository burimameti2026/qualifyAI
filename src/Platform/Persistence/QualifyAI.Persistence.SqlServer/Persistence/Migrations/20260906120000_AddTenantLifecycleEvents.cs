using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Persistence.SqlServer.Migrations
{
    public partial class AddTenantLifecycleEvents : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[TenantLifecycleEvents]', N'U') IS NULL
BEGIN
    CREATE TABLE [TenantLifecycleEvents]
    (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] nvarchar(64) NOT NULL,
        [Status] nvarchar(64) NOT NULL,
        [Message] nvarchar(2000) NOT NULL,
        [DataJson] nvarchar(8000) NULL,
        [CorrelationId] nvarchar(128) NULL,
        [Source] nvarchar(128) NOT NULL,
        [ActorId] nvarchar(256) NULL,
        [OccurredAtUtc] datetime2 NOT NULL,
        [RecordedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_TenantLifecycleEvents] PRIMARY KEY ([Id])
    );

    CREATE INDEX [IX_TenantLifecycleEvents_TenantId_OccurredAtUtc]
        ON [TenantLifecycleEvents] ([TenantId], [OccurredAtUtc]);

    CREATE INDEX [IX_TenantLifecycleEvents_CorrelationId]
        ON [TenantLifecycleEvents] ([CorrelationId]);
END
ELSE
BEGIN
    IF COL_LENGTH(N'[TenantLifecycleEvents]', N'DataJson') IS NOT NULL
        AND EXISTS (
            SELECT 1
            FROM sys.columns c
            JOIN sys.types t ON c.user_type_id = t.user_type_id
            WHERE c.object_id = OBJECT_ID(N'[TenantLifecycleEvents]')
              AND c.name = N'DataJson'
              AND t.name IN (N'nvarchar', N'varchar')
              AND c.max_length <> -1)
    BEGIN
        ALTER TABLE [TenantLifecycleEvents]
            ALTER COLUMN [DataJson] nvarchar(8000) NULL;
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'[TenantLifecycleEvents]')
          AND name = N'IX_TenantLifecycleEvents_TenantId_OccurredAtUtc')
    BEGIN
        CREATE INDEX [IX_TenantLifecycleEvents_TenantId_OccurredAtUtc]
            ON [TenantLifecycleEvents] ([TenantId], [OccurredAtUtc]);
    END;

    IF NOT EXISTS (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'[TenantLifecycleEvents]')
          AND name = N'IX_TenantLifecycleEvents_CorrelationId')
    BEGIN
        CREATE INDEX [IX_TenantLifecycleEvents_CorrelationId]
            ON [TenantLifecycleEvents] ([CorrelationId]);
    END;
END");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
IF OBJECT_ID(N'[TenantLifecycleEvents]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [TenantLifecycleEvents];
END");
        }
    }
}
