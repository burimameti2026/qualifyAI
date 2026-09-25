using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

public partial class AddCampaignPackageVersion : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PackageVersion",
            table: "Campaigns",
            type: "nvarchar(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropColumn(name: "PackageVersion", table: "Campaigns");
}
