using Microsoft.EntityFrameworkCore.Migrations;

namespace LeadsAI.Persistence.SqlServer.Migrations
{
    [Migration("20260926170000_AddCampaignToAutonomousAcquisitionRuns")]
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
