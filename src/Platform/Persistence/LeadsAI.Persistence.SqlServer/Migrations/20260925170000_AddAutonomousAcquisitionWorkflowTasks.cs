using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

public partial class AddAutonomousAcquisitionWorkflowTasks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AutonomousAcquisitionTasks",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Sequence = table.Column<int>(type: "int", nullable: false),
                Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                RequiresApproval = table.Column<bool>(type: "bit", nullable: false),
                ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                Error = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                AttemptCount = table.Column<int>(type: "int", nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AutonomousAcquisitionTasks", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_AgentId_Sequence",
            table: "AutonomousAcquisitionTasks",
            columns: new[] { "TenantId", "AgentId", "Sequence" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AutonomousAcquisitionTasks_TenantId_Status",
            table: "AutonomousAcquisitionTasks",
            columns: new[] { "TenantId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AutonomousAcquisitionTasks");
    }
}
