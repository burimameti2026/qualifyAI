using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations
{
    public partial class BindTargetListsToCampaigns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "TargetLists",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: Guid.Empty);

            migrationBuilder.Sql(@"
UPDATE tl
SET CampaignId = c.Id
FROM TargetLists tl
INNER JOIN Campaigns c ON c.TenantId = tl.TenantId AND c.TargetListId = tl.Id
WHERE tl.CampaignId = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.CreateIndex(
                name: "IX_TargetLists_TenantId_CampaignId_Name",
                table: "TargetLists",
                columns: new[] { "TenantId", "CampaignId", "Name" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_TargetLists_TenantId_CampaignId_Name",
                table: "TargetLists");

            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "TargetLists");
        }
    }
}