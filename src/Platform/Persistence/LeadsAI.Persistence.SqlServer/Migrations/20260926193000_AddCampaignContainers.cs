using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

public partial class AddCampaignContainers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CampaignContainers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CampaignId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                PackageCode = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                PackageVersion = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                Status = table.Column<int>(type: "int", nullable: false),
                ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                LastStartedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                LastStoppedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_CampaignContainers", x => x.Id));

        migrationBuilder.AddColumn<Guid>(
            name: "ContainerId",
            table: "AutonomousAcquisitionAgentRuns",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CampaignContainers_TenantId_CampaignId",
            table: "CampaignContainers",
            columns: new[] { "TenantId", "CampaignId" });

        migrationBuilder.CreateIndex(
            name: "IX_CampaignContainers_TenantId_AgentId",
            table: "CampaignContainers",
            columns: new[] { "TenantId", "AgentId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_AutonomousAcquisitionAgentRuns_ContainerId",
            table: "AutonomousAcquisitionAgentRuns",
            column: "ContainerId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_AutonomousAcquisitionAgentRuns_ContainerId",
            table: "AutonomousAcquisitionAgentRuns");

        migrationBuilder.DropColumn(
            name: "ContainerId",
            table: "AutonomousAcquisitionAgentRuns");

        migrationBuilder.DropTable(
            name: "CampaignContainers");
    }
}
