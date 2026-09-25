using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

public partial class SyncCampaignAndAcquisitionSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid?>(
            name: "AgentId",
            table: "Campaigns",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PackageCode",
            table: "Campaigns",
            type: "nvarchar(128)",
            maxLength: 128,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Objective",
            table: "Campaigns",
            type: "nvarchar(2000)",
            maxLength: 2000,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "PlanJson",
            table: "Campaigns",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<string>(
            name: "PlanStatus",
            table: "Campaigns",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "draft");

        migrationBuilder.DropIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_AgentId_RunId_Sequence",
            table: "AutonomousAcquisitionTasks");

        migrationBuilder.CreateIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_AgentId_RunId_Sequence",
            table: "AutonomousAcquisitionTasks",
            columns: new[] { "TenantId", "AgentId", "RunId", "Sequence" },
            unique: true,
            filter: "[RunId] IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_AgentId_RunId_Sequence",
            table: "AutonomousAcquisitionTasks");

        migrationBuilder.CreateIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_AgentId_RunId_Sequence",
            table: "AutonomousAcquisitionTasks",
            columns: new[] { "TenantId", "AgentId", "RunId", "Sequence" },
            unique: true);

        migrationBuilder.DropColumn(name: "AgentId", table: "Campaigns");
        migrationBuilder.DropColumn(name: "PackageCode", table: "Campaigns");
        migrationBuilder.DropColumn(name: "Objective", table: "Campaigns");
        migrationBuilder.DropColumn(name: "PlanJson", table: "Campaigns");
        migrationBuilder.DropColumn(name: "PlanStatus", table: "Campaigns");
    }
}
