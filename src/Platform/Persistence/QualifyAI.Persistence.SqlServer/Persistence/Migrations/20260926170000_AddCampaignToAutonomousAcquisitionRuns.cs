using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadsAI.Persistence.SqlServer.Migrations
{
    public partial class AddCampaignToAutonomousAcquisitionRuns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "AutonomousAcquisitionAgentRuns",
                type: "uniqueidentifier",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "AutonomousAcquisitionAgentRuns");
        }
    }
}
