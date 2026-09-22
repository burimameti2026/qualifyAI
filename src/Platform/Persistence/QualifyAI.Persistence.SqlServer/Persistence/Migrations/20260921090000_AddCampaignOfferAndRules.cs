using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Persistence.SqlServer.Migrations;

public partial class AddCampaignOfferAndRules : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "OfferId",
            table: "Campaigns",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "RulesJson",
            table: "CampaignSteps",
            type: "nvarchar(max)",
            nullable: false,
            defaultValue: "{}");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "OfferId", table: "Campaigns");
        migrationBuilder.DropColumn(name: "RulesJson", table: "CampaignSteps");
    }
}
