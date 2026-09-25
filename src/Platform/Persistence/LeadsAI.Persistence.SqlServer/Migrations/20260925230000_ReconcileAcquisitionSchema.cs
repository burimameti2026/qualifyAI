using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

public partial class ReconcileAcquisitionSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
IF COL_LENGTH('dbo.Campaigns', 'AgentId') IS NULL
    ALTER TABLE [Campaigns] ADD [AgentId] uniqueidentifier NULL;

IF COL_LENGTH('dbo.Campaigns', 'PackageCode') IS NULL
    ALTER TABLE [Campaigns] ADD [PackageCode] nvarchar(128) NOT NULL CONSTRAINT [DF_Campaigns_PackageCode_Reconcile] DEFAULT N'custom';

IF COL_LENGTH('dbo.Campaigns', 'PackageVersion') IS NULL
    ALTER TABLE [Campaigns] ADD [PackageVersion] nvarchar(32) NOT NULL CONSTRAINT [DF_Campaigns_PackageVersion_Reconcile] DEFAULT N'1.0';

IF COL_LENGTH('dbo.Campaigns', 'Objective') IS NULL
    ALTER TABLE [Campaigns] ADD [Objective] nvarchar(2000) NOT NULL CONSTRAINT [DF_Campaigns_Objective_Reconcile] DEFAULT N'';

IF COL_LENGTH('dbo.Campaigns', 'PlanJson') IS NULL
    ALTER TABLE [Campaigns] ADD [PlanJson] nvarchar(max) NOT NULL CONSTRAINT [DF_Campaigns_PlanJson_Reconcile] DEFAULT N'{}';

IF COL_LENGTH('dbo.Campaigns', 'PlanStatus') IS NULL
    ALTER TABLE [Campaigns] ADD [PlanStatus] nvarchar(32) NOT NULL CONSTRAINT [DF_Campaigns_PlanStatus_Reconcile] DEFAULT N'draft';

IF COL_LENGTH('dbo.TargetLists', 'CampaignId') IS NULL
    ALTER TABLE [TargetLists] ADD [CampaignId] uniqueidentifier NOT NULL CONSTRAINT [DF_TargetLists_CampaignId_Reconcile] DEFAULT '00000000-0000-0000-0000-000000000000';

IF COL_LENGTH('dbo.AutonomousAcquisitionAgentRuns', 'CampaignId') IS NULL
    ALTER TABLE [AutonomousAcquisitionAgentRuns] ADD [CampaignId] uniqueidentifier NOT NULL CONSTRAINT [DF_AutonomousRuns_CampaignId_Reconcile] DEFAULT '00000000-0000-0000-0000-000000000000';

IF COL_LENGTH('dbo.AutonomousAcquisitionTasks', 'RunId') IS NULL
    ALTER TABLE [AutonomousAcquisitionTasks] ADD [RunId] uniqueidentifier NULL;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AutonomousAcquisitionTasks_TenantId_AgentId_RunId_Sequence' AND object_id = OBJECT_ID('dbo.AutonomousAcquisitionTasks'))
    CREATE INDEX [IX_AutonomousAcquisitionTasks_TenantId_AgentId_RunId_Sequence]
    ON [AutonomousAcquisitionTasks] ([TenantId], [AgentId], [RunId], [Sequence]);
""");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // This reconciliation migration is intentionally additive and guarded.
        // Existing databases may have acquired some columns independently.
    }
}
