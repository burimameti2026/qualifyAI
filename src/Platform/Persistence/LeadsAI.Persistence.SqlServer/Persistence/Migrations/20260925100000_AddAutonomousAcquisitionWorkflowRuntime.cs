using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Persistence.Migrations;

public partial class AddAutonomousAcquisitionWorkflowRuntime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH(N'[Campaigns]', N'AgentId') IS NULL
    ALTER TABLE [Campaigns] ADD [AgentId] uniqueidentifier NULL;

IF COL_LENGTH(N'[Campaigns]', N'PackageCode') IS NULL
    ALTER TABLE [Campaigns] ADD [PackageCode] nvarchar(128) NOT NULL CONSTRAINT [DF_Campaigns_PackageCode_Acquisition] DEFAULT N'';

IF COL_LENGTH(N'[Campaigns]', N'Objective') IS NULL
    ALTER TABLE [Campaigns] ADD [Objective] nvarchar(2000) NOT NULL CONSTRAINT [DF_Campaigns_Objective_Acquisition] DEFAULT N'';

IF COL_LENGTH(N'[Campaigns]', N'PlanJson') IS NULL
    ALTER TABLE [Campaigns] ADD [PlanJson] nvarchar(max) NOT NULL CONSTRAINT [DF_Campaigns_PlanJson_Acquisition] DEFAULT N'{}';

IF COL_LENGTH(N'[Campaigns]', N'PlanStatus') IS NULL
    ALTER TABLE [Campaigns] ADD [PlanStatus] nvarchar(32) NOT NULL CONSTRAINT [DF_Campaigns_PlanStatus_Acquisition] DEFAULT N'draft';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Campaigns_TenantId_AgentId' AND object_id = OBJECT_ID(N'[Campaigns]'))
    CREATE INDEX [IX_Campaigns_TenantId_AgentId] ON [Campaigns] ([TenantId], [AgentId]);

IF OBJECT_ID(N'[AutonomousAcquisitionTasks]', N'U') IS NULL
BEGIN
    CREATE TABLE [AutonomousAcquisitionTasks] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AgentId] uniqueidentifier NOT NULL,
        [Sequence] int NOT NULL,
        [Type] nvarchar(64) NOT NULL,
        [Name] nvarchar(200) NOT NULL,
        [Status] int NOT NULL,
        [RequiresApproval] bit NOT NULL,
        [ConfigurationJson] nvarchar(max) NOT NULL,
        [ResultJson] nvarchar(max) NOT NULL,
        [Error] nvarchar(4000) NULL,
        [AttemptCount] int NOT NULL,
        [StartedAtUtc] datetime2 NULL,
        [CompletedAtUtc] datetime2 NULL,
        [CreatedAtUtc] datetime2 NOT NULL,
        [UpdatedAtUtc] datetime2 NOT NULL,
        CONSTRAINT [PK_AutonomousAcquisitionTasks] PRIMARY KEY ([Id])
    );

    CREATE UNIQUE INDEX [IX_AutonomousAcquisitionTasks_TenantId_AgentId_Sequence]
        ON [AutonomousAcquisitionTasks] ([TenantId], [AgentId], [Sequence]);

    CREATE INDEX [IX_AutonomousAcquisitionTasks_TenantId_Status]
        ON [AutonomousAcquisitionTasks] ([TenantId], [Status]);
END;
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF OBJECT_ID(N'[AutonomousAcquisitionTasks]', N'U') IS NOT NULL
    DROP TABLE [AutonomousAcquisitionTasks];

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Campaigns_TenantId_AgentId' AND object_id = OBJECT_ID(N'[Campaigns]'))
    DROP INDEX [IX_Campaigns_TenantId_AgentId] ON [Campaigns];

IF COL_LENGTH(N'[Campaigns]', N'AgentId') IS NOT NULL
    ALTER TABLE [Campaigns] DROP COLUMN [AgentId];

IF COL_LENGTH(N'[Campaigns]', N'PackageCode') IS NOT NULL
    ALTER TABLE [Campaigns] DROP COLUMN [PackageCode];

IF COL_LENGTH(N'[Campaigns]', N'Objective') IS NOT NULL
    ALTER TABLE [Campaigns] DROP COLUMN [Objective];

IF COL_LENGTH(N'[Campaigns]', N'PlanJson') IS NOT NULL
    ALTER TABLE [Campaigns] DROP COLUMN [PlanJson];

IF COL_LENGTH(N'[Campaigns]', N'PlanStatus') IS NOT NULL
    ALTER TABLE [Campaigns] DROP COLUMN [PlanStatus];
""");
    }
}