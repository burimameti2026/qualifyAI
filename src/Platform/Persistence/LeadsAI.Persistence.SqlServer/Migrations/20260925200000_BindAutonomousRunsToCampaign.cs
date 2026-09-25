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

            migrationBuilder.Sql(@"
UPDATE r
SET CampaignId = c.Id
FROM AutonomousAcquisitionAgentRuns r
INNER JOIN Campaigns c ON c.TenantId = r.TenantId AND c.AgentId = r.AgentId
WHERE r.CampaignId = '00000000-0000-0000-0000-000000000000';
");

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