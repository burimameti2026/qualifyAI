using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations
{
    public partial class BindAutonomousRunsToCampaign : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "AutonomousAcquisitionAgentRuns",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.CreateIndex(
                name: "IX_AutonomousAcquisitionAgentRuns_AgentId_CampaignId_ScheduledAtUtc",
                table: "AutonomousAcquisitionAgentRuns",
                columns: new[] { "AgentId", "CampaignId", "ScheduledAtUtc" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AutonomousAcquisitionAgentRuns_AgentId_CampaignId_ScheduledAtUtc",
                table: "AutonomousAcquisitionAgentRuns");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "AutonomousAcquisitionAgentRuns");
        }
    }
}