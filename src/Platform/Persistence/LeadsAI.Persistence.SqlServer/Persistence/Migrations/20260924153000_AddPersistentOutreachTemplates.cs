using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LeadsAI.Persistence.SqlServer.Migrations;

public partial class AddPersistentOutreachTemplates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "OutreachTemplates",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                SubjectTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                BodyTemplate = table.Column<string>(type: "nvarchar(max)", nullable: false),
                IsActive = table.Column<bool>(type: "bit", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_OutreachTemplates", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_OutreachTemplates_TenantId_Name",
            table: "OutreachTemplates",
            columns: new[] { "TenantId", "Name" },
            unique: true);

        migrationBuilder.AddColumn<Guid>(
            name: "TemplateId",
            table: "CampaignSteps",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CampaignSteps_TenantId_TemplateId",
            table: "CampaignSteps",
            columns: new[] { "TenantId", "TemplateId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CampaignSteps_TenantId_TemplateId",
            table: "CampaignSteps");

        migrationBuilder.DropColumn(
            name: "TemplateId",
            table: "CampaignSteps");

        migrationBuilder.DropTable(
            name: "OutreachTemplates");
    }
}
