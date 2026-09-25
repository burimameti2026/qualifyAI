using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

public partial class AddWorkflowRunTaskInstances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "RunId",
            table: "AutonomousAcquisitionTasks",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.DropIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_AgentId_Sequence",
            table: "AutonomousAcquisitionTasks");

        migrationBuilder.CreateIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_AgentId_RunId_Sequence",
            table: "AutonomousAcquisitionTasks",
            columns: new[] { "TenantId", "AgentId", "RunId", "Sequence" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_AgentId_RunId_Sequence",
            table: "AutonomousAcquisitionTasks");

        migrationBuilder.CreateIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_AgentId_Sequence",
            table: "AutonomousAcquisitionTasks",
            columns: new[] { "TenantId", "AgentId", "Sequence" },
            unique: true);

        migrationBuilder.DropColumn(
            name: "RunId",
            table: "AutonomousAcquisitionTasks");
    }
}
