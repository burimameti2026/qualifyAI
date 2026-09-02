using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QualifyAI.Persistence.SqlServer.Migrations;

public partial class AddProspectResearchMetadata : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(name: "Priority", table: "Prospects", type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "ContactReadiness", table: "Prospects", type: "nvarchar(80)", maxLength: 80, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "SuggestedBuyer", table: "Prospects", type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "SizeBand", table: "Prospects", type: "nvarchar(80)", maxLength: 80, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "PainHypothesis", table: "Prospects", type: "nvarchar(max)", nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "Offer", table: "Prospects", type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "SourceUrl", table: "Prospects", type: "nvarchar(2000)", maxLength: 2000, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "VerificationStatus", table: "Prospects", type: "nvarchar(500)", maxLength: 500, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "OutreachStatus", table: "Prospects", type: "nvarchar(80)", maxLength: 80, nullable: false, defaultValue: "");
        migrationBuilder.AddColumn<string>(name: "DatasetOrigin", table: "Prospects", type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Priority", table: "Prospects");
        migrationBuilder.DropColumn(name: "ContactReadiness", table: "Prospects");
        migrationBuilder.DropColumn(name: "SuggestedBuyer", table: "Prospects");
        migrationBuilder.DropColumn(name: "SizeBand", table: "Prospects");
        migrationBuilder.DropColumn(name: "PainHypothesis", table: "Prospects");
        migrationBuilder.DropColumn(name: "Offer", table: "Prospects");
        migrationBuilder.DropColumn(name: "SourceUrl", table: "Prospects");
        migrationBuilder.DropColumn(name: "VerificationStatus", table: "Prospects");
        migrationBuilder.DropColumn(name: "OutreachStatus", table: "Prospects");
        migrationBuilder.DropColumn(name: "DatasetOrigin", table: "Prospects");
    }
}
