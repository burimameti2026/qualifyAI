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
                nullable: false,
                defaultValue: Guid.Empty);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "AutonomousAcquisitionAgentRuns");
        }
    }
}
